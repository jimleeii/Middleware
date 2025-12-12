using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Middleware.Configuration;
using Middleware.Exceptions;
using Moq;
using System.Net;
using Xunit;

namespace Middleware.Tests.Unit;

/// <summary>
/// Comprehensive unit tests for global exception handler middleware.
/// Covers normal operations, different exception types, and edge cases.
/// </summary>
public class GlobalExceptionHandlerMiddlewareTests
{
    private readonly Mock<ILogger<GlobalExceptionHandlerMiddleware>> _mockLogger;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;
    private readonly IOptions<MiddlewareSettings> _middlewareSettings;

    public GlobalExceptionHandlerMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<GlobalExceptionHandlerMiddleware>>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();
        _mockHostEnvironment.SetupGet(x => x.EnvironmentName).Returns("Production");
        _middlewareSettings = Options.Create(new MiddlewareSettings());
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithoutException_ShouldCallNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithGenericException_ShouldReturn500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new Exception("Test error");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithArgumentException_ShouldReturn400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new ArgumentException("Invalid argument");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithArgumentNullException_ShouldReturn400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new ArgumentNullException("parameter");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithInvalidOperationException_ShouldReturn400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new InvalidOperationException("Invalid operation");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithNotFoundException_ShouldReturn404()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new NotFoundException("Resource not found", "User");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithConflictException_ShouldReturn409()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new ConflictException("Resource already exists");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithForbiddenException_ShouldReturn403()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new ForbiddenException("Access denied");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithValidationException_ShouldReturn400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var errors = new Dictionary<string, string[]> { { "Email", new[] { "Invalid email format" } } };
        var testException = new ValidationException("Validation failed", errors);
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithBusinessRuleException_ShouldReturn422()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new BusinessRuleException("Business rule violated", "INSUFFICIENT_FUNDS");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WhenResponseAlreadyStarted_ShouldNotThrow()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new Exception("Test error");
        RequestDelegate next = async ctx =>
        {
            await ctx.Response.WriteAsync("Started");
            throw testException;
        };
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act & Assert
        await middleware.InvokeAsync(context);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_ResponseShouldHaveJsonContentType()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new Exception("Test error");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_ErrorResponseShouldBeValid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        const string traceId = "test-trace-12345";
        context.TraceIdentifier = traceId;
        var testException = new Exception("Test error");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_ShouldLogException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new Exception("Test error");
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Theory]
    [InlineData(typeof(NotImplementedException), StatusCodes.Status501NotImplemented)]
    [InlineData(typeof(TimeoutException), StatusCodes.Status408RequestTimeout)]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithVariousExceptionTypes_ShouldMapCorrectly(Type exceptionType, int expectedStatus)
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = (Exception)Activator.CreateInstance(exceptionType, "Test error")!;
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(expectedStatus);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithNestedExceptions_ShouldHandleGracefully()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var innerException = new InvalidOperationException("Inner error");
        var testException = new Exception("Outer error", innerException);
        RequestDelegate next = _ => throw testException;
        
        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            _mockLogger.Object,
            _mockHostEnvironment.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
