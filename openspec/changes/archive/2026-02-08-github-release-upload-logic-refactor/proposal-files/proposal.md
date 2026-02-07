# Change: Improve GitHub Release creation logic to support pre-existing releases

## Why

The current `CreateGitHubRelease` method in `nukeBuild/Build.Targets.GitHub.cs:44` assumes that releases do not exist before the build runs. In CI/CD pipelines where GitHub releases may be pre-created (e.g., via `actions/create-release`), the `gh release create` command fails when attempting to create a release for an existing tag, causing the entire build pipeline to fail with a process state exception.

## What Changes

- Replace `gh release create` command with `gh release upload` in the `CreateGitHubRelease` method
- Remove `--title` and `--notes` parameters (these are set during release creation)
- Add `--clobber` flag to allow overwriting existing assets
- Update method behavior to upload packages to pre-existing releases rather than creating new releases
- Maintain error handling pattern consistent with other Nuke targets

## Impact

- **Affected specs**: `github-release` (new spec for GitHub release functionality)
- **Affected code**:
  - `nukeBuild/Build.Targets.GitHub.cs:44-74` - `CreateGitHubRelease` method
  - `nukeBuild/Build.Targets.GitHub.cs:76-82` - `BuildReleaseNotes` method (no longer needed for upload)
- **Affected workflows**:
  - CI/CD pipelines that pre-create GitHub releases before uploading artifacts
  - Release orchestration flows where release creation and asset upload are separate steps

## Positive Outcomes

- **Pipeline stability**: Resolves build failures caused by pre-existing releases
- **Workflow flexibility**: Supports multiple release patterns (create-first-then-upload, or upload to existing)
- **Separation of concerns**: Decouples release metadata creation from asset upload

## Risks

- **Low risk**: Only modifies the command execution within a single method
- **Behavioral change**: If a release does not exist, the command will fail (same as current behavior)
- **Testing requirement**: The `--clobber` flag behavior should be verified in CI environment
- **Permissions**: GitHub token must have `contents:write` scope (already required)

## Backward Compatibility

- The change maintains the same failure mode when a release does not exist
- Existing workflows that create releases after asset upload are not affected
- The method signature remains unchanged
