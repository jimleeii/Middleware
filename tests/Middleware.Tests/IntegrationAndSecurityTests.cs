using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Middleware.Configuration;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Middleware.Tests.Unit;

/// <summary>
/// Unit tests for exception handling configuration settings.
/// </summary>
public class ExceptionHandlingSettingsTests
{
    [Fact]
    [Trait("Category", "Configuration")]
    public void ExceptionHandlingSettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var settings = new ExceptionHandlingSettings();

        // Assert
        settings.Should().NotBeNull();
        settings.IncludeExceptionDetails.Should().BeNull();
    }
}

/// <summary>
/// Integration-style tests for configuration validation.
/// </summary>
public class MiddlewareConfigurationValidationTests
{
    [Fact]
    [Trait("Category", "Configuration")]
    public void ValidateMiddlewareSettings_WithValidConfiguration_ShouldPass()
    {
        // Arrange
        var settings = new MiddlewareSettings
        {
            Authentication = new AuthenticationSettings
            {
                ApiKeys = new[] { "valid-api-key-1234567890" },
                MaxFailedAttempts = 5,
                RateLimitWindowMinutes = 15
            },
            CorrelationId = new CorrelationIdSettings
            {
                HeaderName = "X-Correlation-Id",
                MinLength = 8,
                MaxLength = 128
            },
            ExceptionHandling = new ExceptionHandlingSettings()
        };

        // Act & Assert
        var authAct = () => settings.Authentication.Validate();
        var corrAct = () => settings.CorrelationId.Validate();

        authAct.Should().NotThrow();
        corrAct.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void ValidateMiddlewareSettings_WithInvalidApiKey_ShouldThrow()
    {
        // Arrange
        var settings = new MiddlewareSettings
        {
            Authentication = new AuthenticationSettings
            {
                ApiKeys = new[] { "short" }
            }
        };

        // Act & Assert
        var act = () => settings.Authentication.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void ValidateMiddlewareSettings_WithInvalidCorrelationIdSettings_ShouldThrow()
    {
        // Arrange
        var settings = new MiddlewareSettings
        {
            CorrelationId = new CorrelationIdSettings
            {
                MinLength = 200,
                MaxLength = 100
            }
        };

        // Act & Assert
        var act = () => settings.CorrelationId.Validate();
        act.Should().Throw<InvalidOperationException>();
    }
}

/// <summary>
/// Edge case and security-focused tests for correlation ID middleware.
/// </summary>
public class CorrelationIdMiddlewareSecurityTests
{
    private readonly Mock<ILogger<CorrelationIdMiddleware>> _mockLogger;
    private readonly IOptions<MiddlewareSettings> _middlewareSettings;

    public CorrelationIdMiddlewareSecurityTests()
    {
        _mockLogger = new Mock<ILogger<CorrelationIdMiddleware>>();
        _middlewareSettings = Options.Create(new MiddlewareSettings());
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InvokeAsync_WithEmptyCorrelationId_ShouldGenerateNew()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "";

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Response.Headers["X-Correlation-Id"].ToString();
        correlationId.Should().NotBeEmpty();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InvokeAsync_WithWhitespaceOnlyCorrelationId_ShouldGenerateNew()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "   ";

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Response.Headers["X-Correlation-Id"].ToString();
        correlationId.Should().NotBe("   ");
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InvokeAsync_WithSpecialCharactersInId_ShouldReject()
    {
        // Arrange
        const string specialCharsId = "test@special#chars$";
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = specialCharsId;

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var responseId = context.Response.Headers["X-Correlation-Id"].ToString();
        responseId.Should().NotBe(specialCharsId);
    }

    [Theory]
    [InlineData("valid-id-12345")]
    [InlineData("valid_id_12345")]
    [InlineData("validid12345")]
    [InlineData("a-b_c-d_e-f")]
    [Trait("Category", "Security")]
    public async Task InvokeAsync_WithValidAlphanumericIds_ShouldAccept(string validId)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = validId;

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var responseId = context.Response.Headers["X-Correlation-Id"].ToString();
        responseId.Should().Be(validId);
    }
}

/// <summary>
/// Test coverage for middleware integration and concurrency scenarios.
/// </summary>
public class MiddlewareIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MultipleRequests_ShouldEachGetUniqueCorrelationId()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CorrelationIdMiddleware>>();
        var middlewareSettings = Options.Create(new MiddlewareSettings());
        var correlationIds = new List<string>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var context = new DefaultHttpContext();
            var middleware = new CorrelationIdMiddleware(
                _ => Task.CompletedTask,
                mockLogger.Object,
                middlewareSettings);

            await middleware.InvokeAsync(context);
            correlationIds.Add(context.Response.Headers["X-Correlation-Id"].ToString());
        }

        // Assert
        correlationIds.Should().AllSatisfy(id => Guid.TryParse(id, out _).Should().BeTrue());
        correlationIds.Distinct().Count().Should().Be(5);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CorrelationIdMiddleware_ShouldPreserveProvidedIdAcrossMiddleware()
    {
        // Arrange
        const string providedId = "integration-test-id";
        var mockLogger = new Mock<ILogger<CorrelationIdMiddleware>>();
        var middlewareSettings = Options.Create(new MiddlewareSettings());
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = providedId;

        // Act
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            mockLogger.Object,
            middlewareSettings);

        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be(providedId);
    }
}
