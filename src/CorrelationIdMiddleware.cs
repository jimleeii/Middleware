using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Middleware.Configuration;
using Serilog.Context;

namespace Middleware;

/// <summary>
/// Middleware that adds a correlation ID to each request for tracing purposes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">The logger instance for correlation ID middleware.</param>
/// <param name="middlewareSettings">The middleware settings.</param>
public partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger, IOptions<MiddlewareSettings> middlewareSettings)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<CorrelationIdMiddleware> _logger = logger;
    private readonly CorrelationIdSettings _settings = middlewareSettings.Value.CorrelationId;
    
    // Allowed pattern: alphanumeric, hyphens, underscores only (prevents injection attacks)
    private static readonly Regex CorrelationIdPattern = CorrelationIdRegex();

    /// <summary>
    /// Invokes the middleware to add correlation ID.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        // Add to response headers
        if (_settings.IncludeInResponse)
        {
            context.Response.Headers.Append(_settings.HeaderName, correlationId);
        }

        // Add to Serilog context for logging
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    /// <summary>
    /// Gets the correlation ID from the request headers or creates a new one if not found.
    /// Validates incoming correlation IDs to prevent header injection attacks.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The correlation ID.</returns>
    private string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(_settings.HeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            var providedId = correlationId.ToString();
            
            // Validate the provided correlation ID
            if (IsValidCorrelationId(providedId))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Using provided correlation ID: {CorrelationId}",
                        providedId);
                }
                return providedId;
            }
            
            // Log security warning for invalid correlation ID
            if (_settings.LogInvalidIds && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Invalid correlation ID rejected from {IpAddress}. Length: {Length}, Value: {CorrelationId}",
                    GetClientIpAddress(context),
                    providedId.Length,
                    SanitizeForLogging(providedId));
            }
        }

        // Generate new correlation ID
        var newId = Guid.NewGuid().ToString();
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Generated new correlation ID for request from {IpAddress}: {CorrelationId}",
                GetClientIpAddress(context),
                newId);
        }
        return newId;
    }
    
    /// <summary>
    /// Validates a correlation ID to ensure it meets security requirements.
    /// </summary>
    /// <param name="correlationId">The correlation ID to validate.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    private bool IsValidCorrelationId(string correlationId)
    {
        // Check length constraints
        if (correlationId.Length < _settings.MinLength || correlationId.Length > _settings.MaxLength)
        {
            return false;
        }
        
        // Check for invalid characters (prevents header injection)
        if (!CorrelationIdPattern.IsMatch(correlationId))
        {
            return false;
        }
        
        // Additional validation: check for common header injection patterns
        if (correlationId.Contains("\r") || correlationId.Contains("\n") ||
            correlationId.Contains("\0") || correlationId.Contains(":"))
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Gets the client IP address from the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The client IP address as a string.</returns>
    private static string GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
    
    /// <summary>
    /// Sanitizes a value for safe logging by truncating and escaping special characters.
    /// </summary>
    /// <param name="value">The value to sanitize.</param>
    /// <returns>Sanitized value safe for logging.</returns>
    private static string SanitizeForLogging(string value)
    {
        const int maxLogLength = 50;
        
        if (string.IsNullOrEmpty(value))
        {
            return "(empty)";
        }
        
        // Truncate if too long
        var sanitized = value.Length > maxLogLength
            ? value[..maxLogLength] + "..."
            : value;
        
        // Escape control characters for safe logging
        sanitized = sanitized
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\0", "\\0");
        
        return sanitized;
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled)]
    private static partial Regex CorrelationIdRegex();
}
