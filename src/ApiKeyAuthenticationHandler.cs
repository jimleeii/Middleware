using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Middleware.Configuration;

namespace Middleware;

/// <summary>
/// Authentication handler for API Key authentication.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ApiKeyAuthenticationHandler"/> class.
/// </remarks>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    IOptions<MiddlewareSettings> middlewareSettings) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly IConfiguration _configuration = configuration;
    private readonly AuthenticationSettings _authSettings = middlewareSettings.Value.Authentication;
    
    // Rate limiting: Track failed attempts per IP address
    private static readonly ConcurrentDictionary<string, FailedAttemptInfo> _failedAttempts = new();
    
    // Cleanup mechanism for rate limiting entries
    private static readonly object _cleanupLock = new object();
    private static CancellationTokenSource? _cleanupCancellation;
    private static Task? _cleanupTask;
    
    /// <summary>
    /// Initializes the cleanup background task on first usage.
    /// </summary>
    static ApiKeyAuthenticationHandler()
    {
        StartCleanupTask();
    }
    
    /// <summary>
    /// Starts the background cleanup task for expired rate limit entries.
    /// </summary>
    private static void StartCleanupTask()
    {
        lock (_cleanupLock)
        {
            // Only start if not already running
            if (_cleanupTask != null && !_cleanupTask.IsCompleted)
            {
                return;
            }
            
            _cleanupCancellation = new CancellationTokenSource();
            _cleanupTask = Task.Run(() => CleanupExpiredEntriesAsync(_cleanupCancellation.Token), _cleanupCancellation.Token);
        }
    }
    
    /// <summary>
    /// Periodically cleans up expired rate limit entries from the dictionary.
    /// This prevents unbounded memory growth in long-running applications.
    /// </summary>
    private static async Task CleanupExpiredEntriesAsync(CancellationToken cancellationToken)
    {
        // Cleanup interval: Check every 5 minutes
        const int cleanupIntervalMinutes = 5;
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(cleanupIntervalMinutes), cancellationToken).ConfigureAwait(false);
                
                var now = DateTime.UtcNow;
                var keysToRemove = new List<string>();
                
                // Find all entries that have expired (beyond any possible rate limit window)
                // We use a conservative cleanup window of 24 hours to ensure we don't remove
                // entries that might still be within the configured rate limit window
                const int maxCleanupWindowHours = 24;
                var cleanupThreshold = now.AddHours(-maxCleanupWindowHours);
                
                foreach (var kvp in _failedAttempts)
                {
                    if (kvp.Value.FirstAttemptTime < cleanupThreshold)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                // Remove the expired entries
                foreach (var key in keysToRemove)
                {
                    _failedAttempts.TryRemove(key, out _);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the application shuts down
        }
        catch
        {
            // Silently fail for cleanup task - don't crash the application
            // The cleanup is an optimization, not critical to functionality
        }
    }

    /// <summary>
    /// Handles the authentication asynchronously.
    /// </summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var clientIp = GetClientIpAddress();
        
        // Check rate limiting
        if (IsRateLimited(clientIp, _authSettings))
        {
            if (Logger.IsEnabled(LogLevel.Warning))
            {
                Logger.LogWarning(
                    "Rate limit exceeded for IP {IpAddress}. Too many failed authentication attempts.",
                    clientIp);
            }
            return AuthenticateResult.Fail("Too many failed authentication attempts. Please try again later.");
        }
        
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out StringValues value))
        {
            RecordFailedAttempt(clientIp, _authSettings);
            return AuthenticateResult.Fail("API Key header not found");
        }

        var providedApiKey = value.ToString();
        
        // Get API keys from configuration (supports both appsettings and environment variables)
        // Priority: Environment variable > Configuration file
        var validApiKeys = GetValidApiKeys();

        if (validApiKeys == null || validApiKeys.Length == 0)
        {
            Logger.LogWarning("No API keys configured. Authentication will fail for all requests.");
            return AuthenticateResult.Fail("API authentication is not configured");
        }

        // Use constant-time comparison to prevent timing attacks
        if (!IsValidApiKey(providedApiKey, validApiKeys))
        {
            RecordFailedAttempt(clientIp, _authSettings);
            
            if (Logger.IsEnabled(LogLevel.Warning))
            {
                var attemptInfo = _failedAttempts.GetValueOrDefault(clientIp);
                var attemptCount = attemptInfo?.Count ?? 1;
                
                Logger.LogWarning(
                    "Invalid API key attempt #{AttemptCount} from {IpAddress}. {RemainingAttempts} attempts remaining before rate limit.",
                    attemptCount,
                    clientIp,
                    Math.Max(0, _authSettings.MaxFailedAttempts - attemptCount));
            }
            
            return AuthenticateResult.Fail("Invalid API Key");
        }

        // Successful authentication - clear any failed attempts
        ClearFailedAttempts(clientIp);
        
        if (_authSettings.LogSuccessfulAuthentications && Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation("Successful API key authentication from {IpAddress}", clientIp);
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "ApiUser") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Gets valid API keys from environment variables or configuration.
    /// Environment variable takes precedence over configuration file.
    /// </summary>
    /// <returns>Array of valid API keys.</returns>
    private string[] GetValidApiKeys()
    {
        // Try environment variable first (recommended for production)
        var envApiKeys = Environment.GetEnvironmentVariable("API_KEYS");
        if (!string.IsNullOrWhiteSpace(envApiKeys))
        {
            Logger.LogInformation("Using API keys from environment variable");
            return envApiKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Fall back to configuration file
        var configApiKeys = _configuration.GetSection("Authentication:ApiKeys").Get<string[]>();
        if (configApiKeys != null && configApiKeys.Length > 0)
        {
            Logger.LogInformation("Using API keys from configuration file");
            return configApiKeys;
        }

        return [];
    }
    
    /// <summary>
    /// Validates the provided API key against valid keys using constant-time comparison.
    /// This prevents timing attacks that could reveal information about valid API keys.
    /// </summary>
    /// <param name="providedKey">The API key provided by the client.</param>
    /// <param name="validKeys">Array of valid API keys.</param>
    /// <returns>True if the provided key matches any valid key; otherwise, false.</returns>
    private static bool IsValidApiKey(string providedKey, string[] validKeys)
    {
        if (string.IsNullOrEmpty(providedKey))
        {
            return false;
        }
        
        var providedKeyBytes = Encoding.UTF8.GetBytes(providedKey);
        
        foreach (var validKey in validKeys)
        {
            if (string.IsNullOrEmpty(validKey))
            {
                continue;
            }
            
            var validKeyBytes = Encoding.UTF8.GetBytes(validKey);
            
            // Use constant-time comparison to prevent timing attacks
            if (CryptographicOperations.FixedTimeEquals(providedKeyBytes, validKeyBytes))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets the client IP address from the request.
    /// </summary>
    /// <returns>The client IP address as a string.</returns>
    private string GetClientIpAddress()
    {
        // Check for X-Forwarded-For header (when behind a proxy/load balancer)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP if multiple are present
            return forwardedFor.Split(',')[0].Trim();
        }
        
        return Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
    
    /// <summary>
    /// Checks if the given IP address is currently rate limited.
    /// </summary>
    /// <param name="ipAddress">The IP address to check.</param>
    /// <param name="settings">The authentication settings.</param>
    /// <returns>True if rate limited; otherwise, false.</returns>
    private static bool IsRateLimited(string ipAddress, AuthenticationSettings settings)
    {
        if (!_failedAttempts.TryGetValue(ipAddress, out var attemptInfo))
        {
            return false;
        }
        
        var rateLimitWindow = TimeSpan.FromMinutes(settings.RateLimitWindowMinutes);
        
        // Check if the rate limit window has expired
        if (DateTime.UtcNow - attemptInfo.FirstAttemptTime > rateLimitWindow)
        {
            // Window expired, remove the entry and allow the request
            _failedAttempts.TryRemove(ipAddress, out _);
            return false;
        }
        
        return attemptInfo.Count >= settings.MaxFailedAttempts;
    }
    
    /// <summary>
    /// Records a failed authentication attempt for the given IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address to record.</param>
    /// <param name="settings">The authentication settings.</param>
    private static void RecordFailedAttempt(string ipAddress, AuthenticationSettings settings)
    {
        var rateLimitWindow = TimeSpan.FromMinutes(settings.RateLimitWindowMinutes);
        
        _failedAttempts.AddOrUpdate(
            ipAddress,
            _ => new FailedAttemptInfo { Count = 1, FirstAttemptTime = DateTime.UtcNow },
            (_, existing) =>
            {
                // Reset if outside the rate limit window
                if (DateTime.UtcNow - existing.FirstAttemptTime > rateLimitWindow)
                {
                    return new FailedAttemptInfo { Count = 1, FirstAttemptTime = DateTime.UtcNow };
                }
                
                return existing with { Count = existing.Count + 1 };
            });
    }
    
    /// <summary>
    /// Clears failed authentication attempts for the given IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address to clear.</param>
    private static void ClearFailedAttempts(string ipAddress)
    {
        _failedAttempts.TryRemove(ipAddress, out _);
    }
    
    /// <summary>
    /// Gets the current number of tracked rate limit entries.
    /// Useful for monitoring memory usage.
    /// </summary>
    /// <returns>The count of entries currently in the rate limit tracking dictionary.</returns>
    internal static int GetRateLimitEntryCount()
    {
        return _failedAttempts.Count;
    }
    
    /// <summary>
    /// Stops the cleanup background task.
    /// Should be called during application shutdown.
    /// </summary>
    internal static async Task StopCleanupTaskAsync()
    {
        if (_cleanupCancellation != null)
        {
            _cleanupCancellation.Cancel();
            
            if (_cleanupTask != null)
            {
                try
                {
                    await _cleanupTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
            
            _cleanupCancellation.Dispose();
        }
    }
}

/// <summary>
/// Tracks failed authentication attempts for rate limiting.
/// </summary>
record FailedAttemptInfo
{
    /// <summary>
    /// Number of failed attempts.
    /// </summary>
    public int Count { get; init; }
    /// <summary>
    /// Time of the first failed attempt.
    /// </summary>
    public DateTime FirstAttemptTime { get; init; }
}

/// <summary>
/// Options for API Key authentication.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}
