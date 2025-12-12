# CI/CD Pipeline Documentation

This document describes the automated CI/CD pipeline for the Middleware library.

## Overview

The project uses **GitHub Actions** for continuous integration and deployment with the following workflows:

### 1. CI Build Workflow (`ci.yml`)
**Trigger**: Push to `main`/`develop` branches, Pull Requests  
**Purpose**: Automated building, testing, and quality checks

### 2. Release Workflow (`release.yml`)
**Trigger**: Version tags (`v*.*.*`), Manual dispatch  
**Purpose**: Create releases and publish packages

---

## CI Build Workflow

### Jobs

#### 1. **build-and-test**
Runs on: Ubuntu, Windows, macOS (matrix build)

**Steps:**
- ✅ Checkout code with full history
- ✅ Setup .NET 8.0
- ✅ Restore NuGet packages
- ✅ Build solution in Release mode
- ✅ Run all tests with code coverage
- ✅ Upload test results and coverage reports
- ✅ Generate coverage summary for PRs

**Artifacts:**
- Test results (TRX format)
- Code coverage reports (Cobertura XML)

#### 2. **code-quality**
Runs on: Ubuntu

**Steps:**
- ✅ Run `dotnet format` to verify code style
- ✅ Check for formatting violations

#### 3. **security-scan**
Runs on: Ubuntu

**Steps:**
- ✅ Run Trivy vulnerability scanner
- ✅ Upload results to GitHub Security tab

#### 4. **package**
Runs on: Ubuntu  
**Condition**: Only on push to `main` or `develop`

**Steps:**
- ✅ Create NuGet packages (.nupkg and .snupkg)
- ✅ Upload packages as artifacts

---

## Release Workflow

### Jobs

#### 1. **create-release**
Runs on: Ubuntu

**Steps:**
- ✅ Extract version from git tag (e.g., `v1.0.0` → `1.0.0`)
- ✅ Build and test the solution
- ✅ Create NuGet packages with version
- ✅ Generate release notes from git commits
- ✅ Create GitHub Release with packages attached

**Artifacts:**
- GitHub Release with changelog
- NuGet packages (.nupkg)
- Symbol packages (.snupkg)

#### 2. **publish-nuget**
Runs on: Ubuntu  
**Condition**: Only on version tags  
**Requires**: `NUGET_API_KEY` secret

**Steps:**
- ✅ Publish packages to NuGet.org
- ✅ Publish symbols for debugging

#### 3. **publish-github-packages**
Runs on: Ubuntu  
**Permissions**: Packages write

**Steps:**
- ✅ Publish packages to GitHub Packages
- ✅ Accessible via GitHub Package Registry

---

## Required Secrets

Configure these in GitHub Settings → Secrets and variables → Actions:

| Secret | Description | Required For |
|--------|-------------|--------------|
| `NUGET_API_KEY` | NuGet.org API key | Publishing to NuGet.org |
| `GITHUB_TOKEN` | Auto-provided by GitHub | GitHub Packages, Releases |

### Getting NuGet API Key

1. Go to https://www.nuget.org/account/apikeys
2. Create a new API key with push permissions
3. Add it to GitHub Secrets as `NUGET_API_KEY`

---

## Workflow Triggers

### CI Build
```yaml
on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
  workflow_dispatch:  # Manual trigger
```

### Release
```yaml
on:
  push:
    tags:
      - 'v*.*.*'  # e.g., v1.0.0, v2.1.3
  workflow_dispatch:
    inputs:
      version:
        description: 'Version to release'
        required: true
```

---

## Creating a Release

### Method 1: Git Tags (Recommended)

```bash
# Update version in Middleware.csproj
# Update CHANGELOG.md
# Commit changes

git add .
git commit -m "Release v1.0.0"
git push

# Create and push tag
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

### Method 2: Manual Workflow Dispatch

1. Go to GitHub Actions → Release and Publish
2. Click "Run workflow"
3. Enter version (e.g., `1.0.0`)
4. Click "Run workflow"

---

## Pull Request Workflow

When you create a PR:

1. ✅ CI builds on all platforms (Ubuntu, Windows, macOS)
2. ✅ All tests run automatically
3. ✅ Code coverage calculated
4. ✅ Coverage summary posted as PR comment
5. ✅ Code quality checks (formatting)
6. ✅ Security scan results

**Required Checks:**
- All tests must pass
- Build must succeed on all platforms
- Code formatting must be clean

---

## Code Coverage

### Viewing Coverage

- **In PRs**: Coverage summary automatically posted as comment
- **In Actions**: Download coverage artifacts from workflow run
- **Thresholds**: Warning at 60%, Good at 80%

### Running Coverage Locally

```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report (requires reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
```

---

## Package Versioning

### Version Sources (Priority Order)

1. **Git Tag**: `v1.2.3` → Package version `1.2.3`
2. **Workflow Input**: Manual version specification
3. **Project File**: `<Version>` in Middleware.csproj

### Version Format

Follow [Semantic Versioning](https://semver.org/):
- **Major.Minor.Patch** (e.g., `1.0.0`)
- **Major**: Breaking changes
- **Minor**: New features (backward compatible)
- **Patch**: Bug fixes

---

## Troubleshooting

### Build Fails on Specific Platform

Check the workflow run for platform-specific errors:
```bash
# Test locally on Windows
dotnet build --configuration Release

# Test on Linux/macOS via Docker
docker run -v ${PWD}:/app -w /app mcr.microsoft.com/dotnet/sdk:8.0 dotnet build
```

### Tests Fail in CI but Pass Locally

Common causes:
- Time zone differences
- File path separators (Windows vs Unix)
- Environment variable dependencies
- Platform-specific APIs

### Package Upload Fails

**NuGet.org:**
- Verify `NUGET_API_KEY` secret is set
- Check API key permissions (push enabled)
- Ensure package version doesn't already exist

**GitHub Packages:**
- Verify workflow has `packages: write` permission
- Check repository visibility settings

### Coverage Upload Fails

- Ensure tests generate coverage files
- Check coverage file path in workflow
- Verify code-coverage-summary action version

---

## Monitoring

### Workflow Status

- **Dashboard**: GitHub Actions tab
- **Notifications**: Configure in GitHub Settings → Notifications
- **Badges**: README shows CI/CD status

### Build Duration

Typical timings:
- CI Build (all platforms): ~3-5 minutes
- Release: ~2-3 minutes
- Package publish: ~1-2 minutes

---

## Best Practices

### For Developers

✅ Run tests locally before pushing  
✅ Keep commits atomic and well-described  
✅ Update CHANGELOG.md for notable changes  
✅ Ensure code passes `dotnet format` checks  
✅ Review security scan results

### For Releases

✅ Update version in Middleware.csproj  
✅ Update CHANGELOG.md with release notes  
✅ Create descriptive git tag messages  
✅ Test package locally before release  
✅ Verify documentation is current

### For Security

✅ Never commit secrets or API keys  
✅ Review security scan results before merging  
✅ Keep dependencies up to date  
✅ Use dependabot for automated updates  
✅ Monitor GitHub Security advisories

---

## Advanced Configuration

### Adding New Platforms

Edit `ci.yml` matrix:
```yaml
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest, macos-latest, ubuntu-22.04]
```

### Custom Test Filters

```yaml
- name: Run unit tests only
  run: dotnet test --filter "Category=Unit"
```

### Slack Notifications

Add to workflow:
```yaml
- name: Notify Slack
  uses: 8398a7/action-slack@v3
  with:
    status: ${{ job.status }}
    webhook_url: ${{ secrets.SLACK_WEBHOOK }}
```

---

## Migration from Other CI Systems

### From Azure DevOps

GitHub Actions YAML is similar. Key differences:
- `trigger:` → `on:`
- `pool:` → `runs-on:`
- `steps:` structure is similar

### From Jenkins

Map Jenkinsfile stages to workflow jobs:
- `stage()` → `jobs:`
- `steps {}` → `steps:`
- Jenkins plugins → GitHub Actions

---

## Support

For issues with CI/CD:
1. Check workflow run logs in GitHub Actions
2. Review this documentation
3. Open an issue with workflow run link
4. Tag with `ci/cd` label
