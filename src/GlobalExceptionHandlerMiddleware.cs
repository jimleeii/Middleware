using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Middleware.Configuration;
using Middleware.Exceptions;

namespace Middleware;

/// <summary>
/// Global exception handling middleware that catches unhandled exceptions and returns appropriate error responses.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GlobalExceptionHandlerMiddleware"/> class.
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">The logger instance.</param>
/// <param name="env">The hosting environment.</param>
/// <param name="middlewareSettings">The middleware settings.</param>
public class GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger, IHostEnvironment env, IOptions<MiddlewareSettings> middlewareSettings)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger = logger;
    private readonly IHostEnvironment _env = env;
    private readonly ExceptionHandlingSettings _settings = middlewareSettings.Value.ExceptionHandling;

    /// <summary>
    /// Invokes the middleware to handle exceptions.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Handles the exception asynchronously by logging the error and returning an appropriate error response to the client.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="exception">The exception to handle.</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Don't handle if response already started
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Cannot handle exception for {TraceId} - response already started",
                context.TraceIdentifier);
            return;
        }

        var (statusCode, errorCode, userMessage) = GetExceptionDetails(exception);

        // Log with appropriate level based on status code
        LogException(exception, context.TraceIdentifier, statusCode);

        var response = BuildErrorResponse(
            context.TraceIdentifier,
            errorCode,
            userMessage,
            exception);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var includeDetails = _settings.IncludeExceptionDetails ?? _env.IsDevelopment();

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = includeDetails,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    /// <summary>
    /// Gets the HTTP status code, error code, and user-friendly message for an exception.
    /// </summary>
    /// <param name="exception">The exception to analyze.</param>
    /// <returns>A tuple containing status code, error code, and message.</returns>
    private static (HttpStatusCode StatusCode, string ErrorCode, string Message) GetExceptionDetails(Exception exception)
    {
        return exception switch
        {
            // Custom API exceptions
            ApiException apiEx => (apiEx.StatusCode, apiEx.ErrorCode, apiEx.Message),

            // Built-in .NET exceptions mapped to appropriate status codes
            ArgumentException or ArgumentNullException or ArgumentOutOfRangeException
                => (HttpStatusCode.BadRequest, "INVALID_ARGUMENT", exception.Message),

            InvalidOperationException
                => (HttpStatusCode.BadRequest, "INVALID_OPERATION", exception.Message),

            UnauthorizedAccessException
                => (HttpStatusCode.Unauthorized, "UNAUTHORIZED", "Authentication required or access denied."),

            NotImplementedException
                => (HttpStatusCode.NotImplemented, "NOT_IMPLEMENTED", "This functionality is not yet implemented."),

            TimeoutException
                => (HttpStatusCode.RequestTimeout, "TIMEOUT", "The request timed out."),

            OperationCanceledException
                => (HttpStatusCode.RequestTimeout, "CANCELLED", "The operation was cancelled."),

            KeyNotFoundException
                => (HttpStatusCode.NotFound, "NOT_FOUND", "The requested resource was not found."),

            // Server errors
            OutOfMemoryException
                => (HttpStatusCode.InternalServerError, "OUT_OF_MEMORY", "The server ran out of memory processing your request."),

            // Default to internal server error
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred while processing your request.")
        };
    }

    /// <summary>
    /// Logs the exception with appropriate severity based on status code.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="traceId">The trace identifier.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    private void LogException(Exception exception, string traceId, HttpStatusCode statusCode)
    {
        var statusCodeValue = (int)statusCode;

        // Client errors (4xx) - log as warning
        if (statusCodeValue >= 400 && statusCodeValue < 500)
        {
            var logLevel = _settings.LogClientErrorsAsWarnings ? LogLevel.Warning : LogLevel.Error;
            if (_logger.IsEnabled(logLevel))
            {
                _logger.Log(
                    logLevel,
                    exception,
                    "Client error occurred. Status: {StatusCode}, TraceId: {TraceId}, Type: {ExceptionType}",
                    statusCode,
                    traceId,
                    exception.GetType().Name);
            }
        }
        // Server errors (5xx) - log as error
        else
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    exception,
                    "Server error occurred. Status: {StatusCode}, TraceId: {TraceId}, Type: {ExceptionType}",
                    statusCode,
                    traceId,
                    exception.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Builds the error response object.
    /// </summary>
    /// <param name="traceId">The trace identifier.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The exception.</param>
    /// <returns>An object representing the error response.</returns>
    private Dictionary<string, object?> BuildErrorResponse(string traceId, string errorCode, string message, Exception exception)
    {
        var includeDetails = _settings.IncludeExceptionDetails ?? _env.IsDevelopment();

        var response = new Dictionary<string, object?>
        {
            ["traceId"] = traceId,
            ["errorCode"] = errorCode,
            ["message"] = includeDetails ? message : GetProductionMessage(exception),
            ["timestamp"] = DateTime.UtcNow,
            ["type"] = exception.GetType().Name
        };

        // Add validation errors if present
        if (exception is ValidationException validationEx && validationEx.Errors != null)
        {
            response["validationErrors"] = validationEx.Errors;
        }

        // Include detailed information only in development or if configured
        var includeStackTrace = _settings.IncludeStackTrace ?? _env.IsDevelopment();
        if (includeDetails && includeStackTrace)
        {
            response["details"] = new
            {
                stackTrace = _settings.FilterStackTrace ? FilterStackTrace(exception.StackTrace) : exception.StackTrace,
                innerException = exception.InnerException != null
                    ? new
                    {
                        type = exception.InnerException.GetType().Name,
                        message = exception.InnerException.Message,
                        stackTrace = _settings.FilterStackTrace ? FilterStackTrace(exception.InnerException.StackTrace) : exception.InnerException.StackTrace
                    }
                    : null
            };
        }

        return response;
    }

    /// <summary>
    /// Gets a production-safe error message.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>A safe error message for production.</returns>
    private string GetProductionMessage(Exception exception)
    {
        // For custom API exceptions and common client errors, show the actual message
        // For server errors, use generic message
        var statusCode = (int)(exception is ApiException apiEx
            ? apiEx.StatusCode
            : HttpStatusCode.InternalServerError);

        return statusCode < 500
            ? exception.Message
            : _settings.DefaultErrorMessage;
    }

    /// <summary>
    /// Filters stack trace to remove sensitive information and framework internals.
    /// </summary>
    /// <param name="stackTrace">The raw stack trace.</param>
    /// <returns>Filtered stack trace.</returns>
    private static string? FilterStackTrace(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
        {
            return null;
        }

        // Filter out framework internals and keep only relevant application frames
        var lines = stackTrace.Split('\n')
            .Where(line => !line.Contains("Microsoft.AspNetCore") &&
                          !line.Contains("System.Runtime") &&
                          !line.Contains("System.Threading"))
            .ToList();

        return lines.Count > 0 ? string.Join('\n', lines) : stackTrace;
    }
}
