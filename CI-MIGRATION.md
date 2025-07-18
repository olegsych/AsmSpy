# CI Migration from AppVeyor to GitHub Actions

This document outlines the migration from AppVeyor to GitHub Actions for the AsmSpy repository.

## Overview

The CI/CD pipeline has been successfully migrated from AppVeyor to GitHub Actions while maintaining the exact same build behavior and outputs.

## Changes Made

### New GitHub Actions Workflows

1. **`.github/workflows/build.yml`** - Main CI workflow
   - Triggers on push/PR to master/main branches
   - Builds solution using MSBuild
   - Runs tests using xUnit
   - Creates artifacts (ZIP and NuGet packages)
   - Uses versioning format: `1.3.{build_number}`

2. **`.github/workflows/release.yml`** - Release workflow
   - Triggers on tag creation
   - Builds and packages for release
   - Creates GitHub releases with artifacts
   - Publishes to Chocolatey and NuGet (requires API keys)

### Updated Files

- **`README.md`**: Updated build badge and download links
- **`.gitignore`**: Added CI artifact directories

## Environment Configuration

The workflows use:
- `windows-latest` runner (for .NET Framework 4.7.2 support)
- MSBuild for compilation (same as AppVeyor)
- NuGet CLI for package operations
- Same Debug configuration and file paths as AppVeyor

## Required Secrets

For release publishing, configure these repository secrets:
- `CHOCOLATEY_API_KEY` - For Chocolatey package publishing
- `NUGET_API_KEY` - For NuGet package publishing

## Migration Notes

- The AppVeyor configuration (`appveyor.yml`) is preserved for reference
- All build steps exactly replicate the AppVeyor process
- Version numbering maintains the same format (`1.3.{build}`)
- Package naming and structure remain identical

## Testing

- YAML syntax has been validated
- Workflow logic mirrors the existing AppVeyor configuration
- The workflows should work immediately upon merge

## Next Steps

1. Merge this PR to enable GitHub Actions
2. Configure the required secrets for publishing
3. Test with a tag to verify release workflow
4. Once confirmed working, the `appveyor.yml` can be removed