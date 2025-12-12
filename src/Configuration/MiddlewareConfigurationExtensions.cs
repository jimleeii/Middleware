using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace Middleware.Configuration;

/// <summary>
/// Extension methods for configuring middleware services.
/// </summary>
public static class MiddlewareConfigurationExtensions
{
    /// <summary>
    /// Adds and validates middleware configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMiddlewareConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure and validate middleware settings
        services.Configure<MiddlewareSettings>(configuration.GetSection(MiddlewareSettings.SectionName));

        // Add options validation
        services.AddOptions<MiddlewareSettings>()
            .BindConfiguration(MiddlewareSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Add custom validation
        services.AddSingleton<IValidateOptions<MiddlewareSettings>, MiddlewareSettingsValidator>();

        return services;
    }

    /// <summary>
    /// Configures Serilog with recommended settings for the middleware.
    /// </summary>
    /// <param name="loggerConfiguration">The logger configuration to extend.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The logger configuration.</returns>
    public static LoggerConfiguration ConfigureMiddlewareLogging(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var logsPath = configuration["Logging:FilePath"] ?? "logs/middleware-.log";
        var minimumLevel = configuration["Logging:LogLevel:Default"] ?? "Information";

        loggerConfiguration
            .MinimumLevel.Is(ParseLogLevel(minimumLevel))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Middleware")
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logsPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] [CorrelationId: {CorrelationId}] {Message:lj}{NewLine}{Exception}");

        // Add debug output in development
        if (environment.IsDevelopment())
        {
            loggerConfiguration.WriteTo.Debug();
        }

        return loggerConfiguration;
    }

    /// <summary>
    /// Validates middleware configuration at startup and logs warnings for common issues.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder ValidateMiddlewareConfiguration(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IApplicationBuilder>>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<MiddlewareSettings>>().Value;
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        logger.LogInformation("Validating middleware configuration...");

        // Validate authentication settings
        try
        {
            settings.Authentication.Validate();
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Authentication configured with {MaxAttempts} max attempts and {WindowMinutes} minute rate limit window",
                    settings.Authentication.MaxFailedAttempts,
                    settings.Authentication.RateLimitWindowMinutes);

            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Authentication configuration validation failed");
            throw;
        }

        // Validate correlation ID settings
        try
        {
            settings.CorrelationId.Validate();
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Correlation ID configured with header '{HeaderName}', length range: {Min}-{Max}",
                    settings.CorrelationId.HeaderName,
                    settings.CorrelationId.MinLength,
                    settings.CorrelationId.MaxLength);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Correlation ID configuration validation failed");
            throw;
        }

        // Warn about exception handling in production
        var includeDetails = settings.ExceptionHandling.IncludeExceptionDetails ?? environment.IsDevelopment();
        if (includeDetails && !environment.IsDevelopment())
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Exception details are enabled in non-development environment '{EnvironmentName}'. " +
                    "This may expose sensitive information. Set Middleware:ExceptionHandling:IncludeExceptionDetails to false.",
                    environment.EnvironmentName);
            }
        }

        logger.LogInformation("Middleware configuration validation completed successfully");

        return app;
    }

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "critical" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}

/// <summary>
/// Custom validator for middleware settings.
/// </summary>
public class MiddlewareSettingsValidator : IValidateOptions<MiddlewareSettings>
{
    /// <summary>
    /// Validates the middleware settings.
    /// </summary>
    /// <param name="name">The name of the options instance.</param>
    /// <param name="options">The middleware settings to validate.</param>
    /// <returns>The validation result.</returns>
    public ValidateOptionsResult Validate(string? name, MiddlewareSettings options)
    {
        var errors = new List<string>();

        try
        {
            options.Authentication.Validate();
        }
        catch (Exception ex)
        {
            errors.Add($"Authentication validation failed: {ex.Message}");
        }

        try
        {
            options.CorrelationId.Validate();
        }
        catch (Exception ex)
        {
            errors.Add($"Correlation ID validation failed: {ex.Message}");
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
