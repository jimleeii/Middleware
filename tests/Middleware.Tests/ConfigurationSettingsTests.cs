using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Middleware.Configuration;
using Moq;
using Xunit;

namespace Middleware.Tests.Unit;

/// <summary>
/// Comprehensive unit tests for middleware configuration and core middleware components.
/// Tests cover normal operations, edge cases, and security scenarios.
/// </summary>
public class MiddlewareSettingsTests
{
    [Fact]
    [Trait("Category", "Configuration")]
    public void MiddlewareSettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var settings = new MiddlewareSettings();

        // Assert
        settings.Should().NotBeNull();
        settings.Authentication.Should().NotBeNull();
        settings.CorrelationId.Should().NotBeNull();
        settings.ExceptionHandling.Should().NotBeNull();
        settings.CorrelationId.HeaderName.Should().Be("X-Correlation-Id");
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void MiddlewareSettings_SectionName_ShouldBeCorrect()
    {
        // Act & Assert
        MiddlewareSettings.SectionName.Should().Be("Middleware");
    }
}

/// <summary>
/// Unit tests for authentication configuration settings and validation.
/// </summary>
public class AuthenticationSettingsTests
{
    [Fact]
    [Trait("Category", "Configuration")]
    public void AuthenticationSettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var settings = new AuthenticationSettings();

        // Assert
        settings.ApiKeys.Should().NotBeNull().And.BeEmpty();
        settings.MaxFailedAttempts.Should().Be(5);
        settings.RateLimitWindowMinutes.Should().Be(15);
        settings.LogSuccessfulAuthentications.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithValidApiKey_ShouldPass()
    {
        // Arrange
        var settings = new AuthenticationSettings
        {
            ApiKeys = new[] { "valid-api-key-1234567890" }
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithMultipleValidApiKeys_ShouldPass()
    {
        // Arrange
        var settings = new AuthenticationSettings
        {
            ApiKeys = new[]
            {
                "valid-api-key-1234567890",
                "another-valid-key-9876543210",
                "third-secure-key-abcdefghijk"
            }
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithNoApiKeysAndNoEnvVar_ShouldThrow()
    {
        // Arrange
        var settings = new AuthenticationSettings { ApiKeys = [] };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No API keys configured*");
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithTooShortApiKey_ShouldThrow()
    {
        // Arrange
        var settings = new AuthenticationSettings
        {
            ApiKeys = new[] { "short" }
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API key is too short*");
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithEmptyApiKey_ShouldThrow()
    {
        // Arrange
        var settings = new AuthenticationSettings
        {
            ApiKeys = new[] { "" }
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API keys cannot be empty*");
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithWhitespaceApiKey_ShouldThrow()
    {
        // Arrange
        var settings = new AuthenticationSettings
        {
            ApiKeys = new[] { "   " }
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [Trait("Category", "Configuration")]
    public void Validate_WithValidMaxFailedAttempts_ShouldPass(int maxAttempts)
    {
        // Arrange
        var settings = new AuthenticationSettings
        {
            ApiKeys = new[] { "valid-api-key-1234567890" },
            MaxFailedAttempts = maxAttempts
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(720)]
    [InlineData(1440)]
    [Trait("Category", "Configuration")]
    public void Validate_WithValidRateLimitWindow_ShouldPass(int minutes)
    {
        // Arrange
        var settings = new AuthenticationSettings
        {
            ApiKeys = new[] { "valid-api-key-1234567890" },
            RateLimitWindowMinutes = minutes
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().NotThrow();
    }
}

/// <summary>
/// Unit tests for correlation ID configuration settings.
/// </summary>
public class CorrelationIdSettingsTests
{
    [Fact]
    [Trait("Category", "Configuration")]
    public void CorrelationIdSettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var settings = new CorrelationIdSettings();

        // Assert
        settings.Should().NotBeNull();
        settings.MinLength.Should().Be(8);
        settings.MaxLength.Should().Be(128);
        settings.HeaderName.Should().Be("X-Correlation-Id");
        settings.IncludeInResponse.Should().BeTrue();
        settings.LogInvalidIds.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithDefaultValues_ShouldPass()
    {
        // Arrange
        var settings = new CorrelationIdSettings();

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithValidCustomValues_ShouldPass()
    {
        // Arrange
        var settings = new CorrelationIdSettings
        {
            MinLength = 10,
            MaxLength = 256,
            HeaderName = "X-Trace-Id"
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithMinLengthGreaterThanMaxLength_ShouldThrow()
    {
        // Arrange
        var settings = new CorrelationIdSettings
        {
            MinLength = 200,
            MaxLength = 100
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void Validate_WithEmptyHeaderName_ShouldThrow()
    {
        // Arrange
        var settings = new CorrelationIdSettings
        {
            HeaderName = ""
        };

        // Act & Assert
        var act = () => settings.Validate();
        act.Should().Throw<InvalidOperationException>();
    }
}

/// <summary>
/// Unit tests for correlation ID middleware functionality.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private readonly Mock<ILogger<CorrelationIdMiddleware>> _mockLogger;
    private readonly IOptions<MiddlewareSettings> _middlewareSettings;

    public CorrelationIdMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<CorrelationIdMiddleware>>();
        _middlewareSettings = Options.Create(new MiddlewareSettings());
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithoutProvidedId_ShouldGenerateNewGuid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;

        var middleware = new CorrelationIdMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-Correlation-Id");
        var correlationId = context.Response.Headers["X-Correlation-Id"].ToString();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithValidProvidedId_ShouldUseProvidedId()
    {
        // Arrange
        const string providedId = "test-correlation-12345";
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = providedId;

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be(providedId);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithMaliciousInput_ShouldRejectAndGenerateNew()
    {
        // Arrange
        const string maliciousId = "test\ninjection";
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = maliciousId;
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var responseId = context.Response.Headers["X-Correlation-Id"].ToString();
        responseId.Should().NotBe(maliciousId);
        Guid.TryParse(responseId, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("test<script>alert</script>")]
    [InlineData("test'; DROP TABLE--")]
    [InlineData("test\r\ninjection")]
    [InlineData("test|command")]
    [InlineData("../../../etc/passwd")]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithSecurityThreats_ShouldReject(string threatPayload)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = threatPayload;
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var responseId = context.Response.Headers["X-Correlation-Id"].ToString();
        responseId.Should().NotBe(threatPayload);
        responseId.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithTooLongId_ShouldReject()
    {
        // Arrange
        var longId = new string('a', 1000);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = longId;

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var responseId = context.Response.Headers["X-Correlation-Id"].ToString();
        responseId.Should().NotBe(longId);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithTooShortId_ShouldReject()
    {
        // Arrange
        const string shortId = "abc";
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = shortId;

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var responseId = context.Response.Headers["X-Correlation-Id"].ToString();
        responseId.Should().NotBe(shortId);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        var middleware = new CorrelationIdMiddleware(
            next,
            _mockLogger.Object,
            _middlewareSettings);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WhenNextThrowsException_ShouldPropagateException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var testException = new InvalidOperationException("Test error");
        RequestDelegate next = _ => throw testException;

        var middleware = new CorrelationIdMiddleware(
            next,
            _mockLogger.Object,
            _middlewareSettings);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithIncludeInResponseFalse_ShouldNotAddToResponse()
    {
        // Arrange
        var settings = new MiddlewareSettings
        {
            CorrelationId = new CorrelationIdSettings { IncludeInResponse = false }
        };
        var middlewareSettingsNoResponse = Options.Create(settings);
        var context = new DefaultHttpContext();

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            middlewareSettingsNoResponse);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("X-Correlation-Id");
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_WithCustomHeaderName_ShouldUseCustomName()
    {
        // Arrange
        const string customHeaderName = "X-Trace-Id";
        var settings = new MiddlewareSettings
        {
            CorrelationId = new CorrelationIdSettings { HeaderName = customHeaderName }
        };
        var middlewareSettingsCustom = Options.Create(settings);
        var context = new DefaultHttpContext();

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            _mockLogger.Object,
            middlewareSettingsCustom);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey(customHeaderName);
    }
}
