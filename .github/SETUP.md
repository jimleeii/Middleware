# CI/CD Setup Guide

Quick reference for setting up the CI/CD pipeline for this project.

## Initial Setup (One-Time)

### 1. Push Code to GitHub

```bash
# Initialize git (if not already done)
git init

# Add remote repository
git remote add origin https://github.com/YOUR_USERNAME/middleware.git

# Add all files
git add .

# Commit
git commit -m "Initial commit with CI/CD pipeline"

# Push to GitHub
git push -u origin main
```

### 2. Configure GitHub Secrets

Go to: **Settings → Secrets and variables → Actions → New repository secret**

#### Required Secrets:

| Secret Name | How to Get | Used For |
|-------------|-----------|----------|
| `NUGET_API_KEY` | https://www.nuget.org/account/apikeys | Publishing to NuGet.org |

#### Getting NuGet API Key:
1. Sign in to https://www.nuget.org
2. Go to Account → API Keys
3. Create New
   - Key Name: `Middleware GitHub Actions`
   - Expiration: 365 days (or custom)
   - Scopes: Select `Push`
   - Glob Pattern: `Middleware.*`
4. Copy the generated key
5. Add to GitHub Secrets as `NUGET_API_KEY`

### 3. Enable GitHub Actions

The workflows will automatically activate when you push to GitHub.

Check: **Actions tab** in your GitHub repository

### 4. Update Badges in README

Replace placeholders in README.md:
```markdown
[![CI Build](https://github.com/YOUR_ORG/middleware/actions/workflows/ci.yml/badge.svg)]
```

Change `YOUR_ORG` and `middleware` to match your repository.

---

## Workflow Files

All workflow files are in `.github/workflows/`:

- **`ci.yml`** - Continuous Integration (builds, tests, code quality)
- **`release.yml`** - Release automation (publish packages)

---

## Day-to-Day Usage

### For Development

1. Create feature branch
```bash
git checkout -b feature/my-feature
```

2. Make changes, commit
```bash
git add .
git commit -m "Add feature X"
git push origin feature/my-feature
```

3. Open Pull Request on GitHub
4. CI runs automatically
5. Review coverage and test results
6. Merge when approved

### For Releases

#### Method 1: Git Tags (Recommended)

```bash
# 1. Update version in src/Middleware.csproj
# <Version>1.1.0</Version>

# 2. Update CHANGELOG.md

# 3. Commit changes
git add .
git commit -m "Release v1.1.0"
git push

# 4. Create and push tag
git tag -a v1.1.0 -m "Release version 1.1.0"
git push origin v1.1.0
```

#### Method 2: Manual Workflow

1. Go to **Actions → Release and Publish**
2. Click **Run workflow**
3. Enter version (e.g., `1.1.0`)
4. Click **Run workflow**

---

## What Happens Automatically

### On Every Push/PR
✅ Build on Ubuntu, Windows, macOS  
✅ Run all tests  
✅ Calculate code coverage  
✅ Check code formatting  
✅ Security vulnerability scan  
✅ Post coverage to PR (for PRs)

### On Version Tags (v*.*.*)
✅ Create GitHub Release  
✅ Generate release notes  
✅ Build NuGet packages  
✅ Publish to NuGet.org  
✅ Publish to GitHub Packages

---

## Troubleshooting

### "NUGET_API_KEY secret not found"

**Solution:**
1. Go to repository Settings → Secrets and variables → Actions
2. Click New repository secret
3. Name: `NUGET_API_KEY`
4. Value: Your API key from nuget.org
5. Click Add secret

### CI Build Fails

**Check:**
1. Go to Actions tab
2. Click on failed workflow run
3. Expand failed job
4. Read error message
5. Fix locally and push again

### Tests Pass Locally but Fail in CI

**Common causes:**
- Platform-specific code (Windows vs Linux)
- Time zone issues
- File path separators
- Missing environment variables

**Fix:**
```bash
# Test on Linux via Docker
docker run -v ${PWD}:/app -w /app mcr.microsoft.com/dotnet/sdk:8.0 dotnet test
```

### Release Workflow Doesn't Trigger

**Check:**
- Tag format must be `v*.*.*` (e.g., `v1.0.0`)
- Tag must be pushed: `git push origin v1.0.0`
- Check Actions tab for any errors

---

## Monitoring

### Build Status
- Green checkmark ✅ = All checks passed
- Red X ❌ = Something failed
- Yellow circle 🟡 = Running

### Where to Check
- **PR**: See checks at bottom of PR
- **Commits**: Checkmark/X next to commit message
- **Actions Tab**: Full workflow history
- **README**: Badges show current status

### Email Notifications

Configure in: **GitHub Settings → Notifications**

---

## Advanced

### Adding Environment-Specific Builds

Edit `.github/workflows/ci.yml`:
```yaml
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest, macos-latest]
    dotnet-version: ['8.0.x', '9.0.x']
```

### Running Workflow Manually

1. Go to Actions tab
2. Select workflow (CI Build or Release)
3. Click "Run workflow"
4. Select branch
5. Click "Run workflow"

### Skipping CI for a Commit

Add to commit message:
```bash
git commit -m "Update docs [skip ci]"
```

---

## Next Steps

1. ✅ Set up NuGet API key secret
2. ✅ Push code to GitHub
3. ✅ Verify CI runs successfully
4. ✅ Update README badges
5. ✅ Create first release tag

## Support

- **CI/CD Documentation**: `.github/CICD.md`
- **Contributing Guide**: `CONTRIBUTING.md`
- **Issues**: Open an issue with `ci/cd` label
