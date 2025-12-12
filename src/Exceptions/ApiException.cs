using System.Net;

namespace Middleware.Exceptions;

/// <summary>
/// Base class for all API exceptions with HTTP status code support.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ApiException"/> class.
/// </remarks>
/// <param name="message">The error message.</param>
/// <param name="statusCode">The HTTP status code.</param>
/// <param name="errorCode">The error code.</param>
/// <param name="innerException">The inner exception.</param>
public abstract class ApiException(
    string message,
    HttpStatusCode statusCode,
    string errorCode,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the HTTP status code associated with this exception.
    /// </summary>
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>
    /// Gets the error code for this exception.
    /// </summary>
    public string ErrorCode { get; } = errorCode;
}

/// <summary>
/// Exception thrown when a requested resource is not found.
/// </summary>
public class NotFoundException(string message, string? resourceName = null) : ApiException(
        message,
        HttpStatusCode.NotFound,
        resourceName != null ? $"NOT_FOUND_{resourceName.ToUpperInvariant()}" : "NOT_FOUND")
{
}

/// <summary>
/// Exception thrown when input validation fails.
/// </summary>
public class ValidationException(string message, IDictionary<string, string[]>? errors = null) : ApiException(message, HttpStatusCode.BadRequest, "VALIDATION_ERROR")
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IDictionary<string, string[]>? Errors { get; } = errors;
}

/// <summary>
/// Exception thrown when a business rule is violated.
/// </summary>
public class BusinessRuleException(string message, string? ruleCode = null) : ApiException(
        message,
        HttpStatusCode.UnprocessableEntity,
        ruleCode ?? "BUSINESS_RULE_VIOLATION")
{
}

/// <summary>
/// Exception thrown when a resource conflict occurs (e.g., duplicate key).
/// </summary>
public class ConflictException(string message) : ApiException(message, HttpStatusCode.Conflict, "CONFLICT")
{
}

/// <summary>
/// Exception thrown when access to a resource is forbidden.
/// </summary>
public class ForbiddenException(string message) : ApiException(message, HttpStatusCode.Forbidden, "FORBIDDEN")
{
}
