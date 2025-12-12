# Middleware.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/Middleware.AspNetCore.svg)](https://www.nuget.org/packages/Middleware.AspNetCore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Version:** 1.0.0 | **Target:** .NET 10.0+ | **Status:** Production Ready ✅

Production-ready ASP.NET Core middleware components for authentication, correlation tracking, and exception handling.

## Features

- **API Key Authentication** - Secure authentication with rate limiting and constant-time comparison
- **Correlation ID Tracking** - Request correlation with security validation
- **Global Exception Handling** - Comprehensive exception handling with custom error types
- **Configuration Validation** - Startup validation with sensible defaults

## Installation

Add the middleware to your project:

```bash
dotnet add reference path/to/Middleware.csproj
```

## Configuration

### 1. Add Configuration to appsettings.json

```json
{
  "Middleware": {
    "Authentication": {
      "ApiKeys": ["your-secure-api-key-minimum-16-characters"],
      "MaxFailedAttempts": 5,
      "RateLimitWindowMinutes": 15,
      "LogSuccessfulAuthentications": true
    },
    "CorrelationId": {
      "HeaderName": "X-Correlation-Id",
      "MinLength": 8,
      "MaxLength": 128,
      "IncludeInResponse": true,
      "LogInvalidIds": true
    },
    "ExceptionHandling": {
      "FilterStackTrace": true,
      "DefaultErrorMessage": "An error occurred while processing your request.",
      "LogClientErrorsAsWarnings": true
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "FilePath": "logs/middleware-.log"
  }
}
```

### 2. Environment Variables (Recommended for Production)

For production environments, use environment variables for sensitive settings:

```bash
# API Keys (comma-separated)
export API_KEYS="key1-min-16-chars,key2-min-16-chars,key3-min-16-chars"
```

## Usage

### Configure Services in Program.cs

```csharp
using Middleware.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ConfigureMiddlewareLogging(builder.Configuration, builder.Environment)
    .CreateLogger();

builder.Host.UseSerilog();

// Add middleware configuration with validation
builder.Services.AddMiddlewareConfiguration(builder.Configuration);

// Add authentication
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        "ApiKey", 
        options => { });

var app = builder.Build();

// Validate configuration at startup
app.ValidateMiddlewareConfiguration();

// Add middleware to pipeline (order matters!)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseAuthentication();

app.Run();
```

## Middleware Components

### API Key Authentication

Provides secure API key-based authentication with:
- Constant-time comparison (prevents timing attacks)
- Rate limiting (5 failed attempts per 15 minutes by default)
- IP-based tracking
- Support for X-Forwarded-For header
- Comprehensive security logging

**Usage:**
```csharp
app.UseAuthentication();

// In controllers
[Authorize(AuthenticationSchemes = "ApiKey")]
public class SecureController : ControllerBase
{
    // Your endpoints
}
```

### Correlation ID Middleware

Adds correlation IDs to requests for distributed tracing:
- Validates incoming correlation IDs
- Prevents header injection attacks
- Automatic GUID generation
- Serilog integration
- Configurable header name and validation rules

**Request Header:**
```
X-Correlation-Id: 550e8400-e29b-41d4-a716-446655440000
```

### Global Exception Handler

Catches and handles all unhandled exceptions:
- Custom exception types (ValidationException, NotFoundException, etc.)
- Environment-aware responses
- Structured error responses
- Client vs server error distinction
- Filtered stack traces
- Production-safe error messages

**Custom Exceptions:**
```csharp
using Middleware.Exceptions;

// Throw custom exceptions
throw new NotFoundException("User not found", "User");
throw new ValidationException("Invalid input", new Dictionary<string, string[]> 
{
    ["Email"] = new[] { "Invalid email format" }
});
throw new BusinessRuleException("Insufficient balance", "INSUFFICIENT_BALANCE");
```

## Configuration Options

### Authentication Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `ApiKeys` | `[]` | Array of valid API keys (min 16 chars) |
| `MaxFailedAttempts` | `5` | Max failed attempts before rate limiting |
| `RateLimitWindowMinutes` | `15` | Rate limit window duration |
| `LogSuccessfulAuthentications` | `true` | Log successful auth attempts |

### Correlation ID Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `HeaderName` | `X-Correlation-Id` | HTTP header name |
| `MinLength` | `8` | Minimum correlation ID length |
| `MaxLength` | `128` | Maximum correlation ID length |
| `IncludeInResponse` | `true` | Include in response headers |
| `LogInvalidIds` | `true` | Log invalid correlation IDs |

### Exception Handling Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `IncludeExceptionDetails` | `null` | Include details (auto-detected by environment) |
| `IncludeStackTrace` | `null` | Include stack trace (auto-detected) |
| `FilterStackTrace` | `true` | Filter framework frames |
| `DefaultErrorMessage` | `"An error occurred..."` | Production error message |
| `LogClientErrorsAsWarnings` | `true` | Log 4xx as warnings |

## Security Best Practices

1. **API Keys**: 
   - Use environment variables in production (`API_KEYS`)
   - Minimum 16 characters
   - Rotate regularly
   - Never commit keys to source control

2. **Rate Limiting**:
   - Default: 5 attempts per 15 minutes
   - Adjust based on your use case
   - Monitor failed attempt logs

3. **Exception Handling**:
   - Never enable `IncludeExceptionDetails` in production
   - Use custom exceptions for domain errors
   - Monitor error logs

4. **Correlation IDs**:
   - Validates format to prevent injection
   - Logs suspicious attempts
   - Automatically sanitized

## Logging

The middleware uses Serilog with:
- Console output with timestamps
- Rolling file logs (30-day retention)
- Correlation ID enrichment
- Environment tagging
- Structured logging

**Log Locations:**
- Development: Console + `logs/middleware-{Date}.log`
- Production: Console + File (configure as needed)

## Validation

Configuration is validated at startup. Common errors:

```
❌ No API keys configured
   → Set Middleware:Authentication:ApiKeys or API_KEYS environment variable

❌ API key too short
   → Minimum 16 characters required for security

❌ Invalid correlation ID header name
   → Use only alphanumeric and hyphens

❌ MinLength > MaxLength
   → Fix correlation ID length constraints
```

## Error Response Format

```json
{
  "traceId": "0HN1234567890ABCDEF",
  "errorCode": "VALIDATION_ERROR",
  "message": "Validation failed",
  "timestamp": "2025-12-11T10:30:00Z",
  "type": "ValidationException",
  "validationErrors": {
    "Email": ["Invalid email format"],
    "Age": ["Must be 18 or older"]
  }
}
```
## Testing

The middleware library includes a comprehensive test suite with **64 tests** achieving 100% pass rate:

### Test Coverage
- **Configuration Tests** (14 tests): Settings validation, defaults, API keys
- **Exception Handling** (25 tests): Status code mapping, error responses, custom exceptions
- **Security Tests** (12 tests): Injection prevention, input validation, concurrent requests
- **Integration Tests** (13 tests): Middleware pipeline, correlation tracking

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific category
dotnet test --filter "Category=Security"
```

### Test Results
- ✅ **64 total tests** - 100% pass rate
- ✅ **80%+ code coverage** across all components
- ✅ **100% security function coverage** for critical paths

## Publishing to NuGet

### Build Package

```bash
# Clean and build
dotnet clean -c Release
dotnet pack -c Release

# Package created in: bin/Release/
# - Middleware.AspNetCore.1.0.0.nupkg
# - Middleware.AspNetCore.1.0.0.snupkg (symbols)
```

### Publish to NuGet.org

```bash
dotnet nuget push bin/Release/Middleware.AspNetCore.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### Publish to Private Feed

```bash
# Azure Artifacts
dotnet nuget push bin/Release/Middleware.AspNetCore.1.0.0.nupkg \
  --api-key az \
  --source https://pkgs.dev.azure.com/{org}/_packaging/{feed}/nuget/v3/index.json

# GitHub Packages
dotnet nuget push bin/Release/Middleware.AspNetCore.1.0.0.nupkg \
  --api-key YOUR_GITHUB_PAT \
  --source https://nuget.pkg.github.com/{owner}/index.json
```

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork and Branch**: Create a feature branch from `main`
2. **Code Standards**: Follow Microsoft C# conventions, add XML docs
3. **Testing**: Maintain 80%+ code coverage, include security tests
4. **Pull Request**: Provide clear description of changes

### Development Setup

```bash
# Clone and build
git clone https://github.com/yourorg/middleware.git
cd middleware
dotnet restore
dotnet build

# Run tests
dotnet test

# Format code
dotnet format
```

### Test Requirements
- Unit tests for all public methods
- Integration tests for middleware pipeline
- Security tests for validation logic
- Minimum 80% code coverage

## Changelog

### [1.0.0] - 2025-12-11

#### Added
- **API Key Authentication Handler**
  - Constant-time comparison preventing timing attacks
  - IP-based rate limiting (5 attempts per 15 minutes)
  - Environment variable and configuration support
  - X-Forwarded-For header support for proxies

- **Correlation ID Middleware**
  - Automatic GUID generation and validation
  - Header injection attack prevention
  - Serilog integration for distributed tracing
  - Configurable header name and length constraints

- **Global Exception Handler**
  - Custom domain exceptions (NotFoundException, ValidationException, etc.)
  - Environment-aware error responses
  - Filtered stack traces for production safety
  - Structured JSON error responses

- **Configuration System**
  - Strongly-typed settings with data annotations
  - Startup validation with fail-fast behavior
  - Sensible defaults for all options

#### Security
- Constant-time API key comparison
- Rate limiting for brute force prevention
- Correlation ID validation prevents injection
- Production-safe error messages

## Versioning

This project follows [Semantic Versioning](https://semver.org/):
- **MAJOR**: Breaking changes
- **MINOR**: New features (backwards compatible)
- **PATCH**: Bug fixes (backwards compatible)

### Compatibility

| Version | .NET Target | ASP.NET Core | Serilog | Status |
|---------|-------------|--------------|---------|--------|
| 1.0.0   | .NET 10.0+   | 8.0+         | 8.0+    | Current |
## License

MIT License - See LICENSE file for details
