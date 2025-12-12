using System.ComponentModel.DataAnnotations;

namespace Middleware.Configuration;

/// <summary>
/// Root configuration settings for the middleware library.
/// </summary>
public class MiddlewareSettings
{
    /// <summary>
    /// Configuration section name for middleware settings.
    /// </summary>
    public const string SectionName = "Middleware";

    /// <summary>
    /// Gets or sets the authentication settings.
    /// </summary>
    public AuthenticationSettings Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the correlation ID settings.
    /// </summary>
    public CorrelationIdSettings CorrelationId { get; set; } = new();

    /// <summary>
    /// Gets or sets the exception handling settings.
    /// </summary>
    public ExceptionHandlingSettings ExceptionHandling { get; set; } = new();
}

/// <summary>
/// Settings for authentication configuration.
/// </summary>
public class AuthenticationSettings
{
    /// <summary>
    /// Gets or sets the API keys for authentication.
    /// Can be overridden by the API_KEYS environment variable.
    /// </summary>
    public string[] ApiKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum number of failed authentication attempts before rate limiting.
    /// Default: 5 attempts.
    /// </summary>
    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the rate limit window in minutes.
    /// Default: 15 minutes.
    /// </summary>
    [Range(1, 1440)]
    public int RateLimitWindowMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets whether to log successful authentication attempts.
    /// Default: true.
    /// </summary>
    public bool LogSuccessfulAuthentications { get; set; } = true;

    /// <summary>
    /// Validates the authentication settings.
    /// </summary>
    public void Validate()
    {
        // Check if API keys are configured (either in config or environment)
        var envApiKeys = Environment.GetEnvironmentVariable("API_KEYS");
        if ((ApiKeys == null || ApiKeys.Length == 0) && string.IsNullOrWhiteSpace(envApiKeys))
        {
            throw new InvalidOperationException(
                "No API keys configured. Configure Authentication:ApiKeys in appsettings.json or set the API_KEYS environment variable.");
        }

        // Validate API key format if configured
        if (ApiKeys != null && ApiKeys.Length > 0)
        {
            foreach (var key in ApiKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new InvalidOperationException("API keys cannot be empty or whitespace.");
                }

                if (key.Length < 16)
                {
                    throw new InvalidOperationException(
                        $"API key is too short. Minimum length is 16 characters for security. Found: {key.Length} characters.");
                }
            }
        }
    }
}

/// <summary>
/// Settings for correlation ID configuration.
/// </summary>
public class CorrelationIdSettings
{
    /// <summary>
    /// Gets or sets the header name for correlation ID.
    /// Default: "X-Correlation-Id".
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    public string HeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>
    /// Gets or sets the minimum allowed length for correlation IDs.
    /// Default: 8 characters.
    /// </summary>
    [Range(1, 256)]
    public int MinLength { get; set; } = 8;

    /// <summary>
    /// Gets or sets the maximum allowed length for correlation IDs.
    /// Default: 128 characters.
    /// </summary>
    [Range(1, 512)]
    public int MaxLength { get; set; } = 128;

    /// <summary>
    /// Gets or sets whether to include correlation ID in response headers.
    /// Default: true.
    /// </summary>
    public bool IncludeInResponse { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log warnings for invalid correlation IDs.
    /// Default: true.
    /// </summary>
    public bool LogInvalidIds { get; set; } = true;

    /// <summary>
    /// Validates the correlation ID settings.
    /// </summary>
    public void Validate()
    {
        if (MinLength > MaxLength)
        {
            throw new InvalidOperationException(
                $"CorrelationId:MinLength ({MinLength}) cannot be greater than MaxLength ({MaxLength}).");
        }

        if (string.IsNullOrWhiteSpace(HeaderName))
        {
            throw new InvalidOperationException("CorrelationId:HeaderName cannot be empty.");
        }

        // Validate header name format (basic HTTP header validation)
        if (!System.Text.RegularExpressions.Regex.IsMatch(HeaderName, "^[a-zA-Z0-9-]+$"))
        {
            throw new InvalidOperationException(
                $"CorrelationId:HeaderName '{HeaderName}' contains invalid characters. Use only alphanumeric and hyphens.");
        }
    }
}

/// <summary>
/// Settings for exception handling configuration.
/// </summary>
public class ExceptionHandlingSettings
{
    /// <summary>
    /// Gets or sets whether to include exception details in responses.
    /// This should only be true in development environments.
    /// Default: false (determined by environment).
    /// </summary>
    public bool? IncludeExceptionDetails { get; set; }

    /// <summary>
    /// Gets or sets whether to include stack traces in responses.
    /// This should only be true in development environments.
    /// Default: false (determined by environment).
    /// </summary>
    public bool? IncludeStackTrace { get; set; }

    /// <summary>
    /// Gets or sets whether to filter framework-specific frames from stack traces.
    /// Default: true.
    /// </summary>
    public bool FilterStackTrace { get; set; } = true;

    /// <summary>
    /// Gets or sets the default error message for production environments.
    /// Default: "An error occurred while processing your request."
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(500)]
    public string DefaultErrorMessage { get; set; } = "An error occurred while processing your request.";

    /// <summary>
    /// Gets or sets whether to log client errors (4xx) as warnings instead of errors.
    /// Default: true.
    /// </summary>
    public bool LogClientErrorsAsWarnings { get; set; } = true;
}
