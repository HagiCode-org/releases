## ADDED Requirements

### Requirement: GitHub Release Asset Upload

The system SHALL provide the ability to upload release artifacts to pre-existing GitHub releases using the `gh release upload` command. GitHub release tags SHALL always use the `v` prefix (e.g., `v0.1.0-beta.1`).

#### Scenario: Upload assets to pre-existing release

- **WHEN** a GitHub release with tag `v{Version}` already exists
- **AND** the `GitHubRelease` target is executed
- **THEN** the system SHALL upload all `.zip` packages from the download directory to the existing release
- **AND** existing assets SHALL be overwritten using the `--clobber` flag
- **AND** the version tag SHALL always include the `v` prefix for GitHub releases

#### Scenario: Upload with valid GitHub token

- **WHEN** a valid `GITHUB_TOKEN` with `contents:write` permission is provided
- **AND** the `GitHubRelease` target is executed
- **THEN** the upload SHALL complete successfully
- **AND** the exit code SHALL be `0`

#### Scenario: No GitHub token available

- **WHEN** the `GITHUB_TOKEN` is not available or empty
- **THEN** the system SHALL log a warning message "GitHub token not available, skipping release creation"
- **AND** the target SHALL return without executing the upload

### Requirement: GitHub Release Upload Command

The `CreateGitHubRelease` method SHALL use `gh release upload` instead of `gh release create`.

#### Scenario: Command format

- **WHEN** building the `gh release upload` command
- **THEN** the format SHALL be: `gh release upload {GitHubReleaseVersion} {packageArgs} --repo {repository} --clobber`
- **AND** `{GitHubReleaseVersion}` SHALL be the version with `v` prefix (e.g., `v0.1.0-beta.1`)
- **AND** `{packageArgs}` SHALL contain space-separated quoted paths to all `.zip` files
- **AND** the `--clobber` flag SHALL be included to overwrite existing assets

#### Scenario: Removed parameters

- **WHEN** executing the upload command
- **THEN** the `--title` parameter SHALL NOT be included (set during release creation)
- **AND** the `--notes` parameter SHALL NOT be included (set during release creation)
- **AND** the `--prerelease` flag SHALL NOT be included (set during release creation)

### Requirement: Upload Error Handling

The system SHALL handle upload failures consistently with other Nuke targets.

#### Scenario: Upload failure with non-zero exit code

- **WHEN** the `gh release upload` command returns a non-zero exit code
- **THEN** the system SHALL throw an exception with message "GitHub Release creation failed"
- **AND** the exception SHALL prevent subsequent targets from executing

#### Scenario: No packages available

- **WHEN** no `.zip` files are found in the download directory
- **THEN** the system SHALL throw an exception with message "No .zip packages found for release upload"
- **AND** the upload SHALL NOT be attempted

### Requirement: Repository Configuration

The system SHALL validate required GitHub repository configuration before attempting upload.

#### Scenario: Missing repository parameter

- **WHEN** the `GitHubRepository` parameter is empty or null
- **THEN** the system SHALL throw an exception with message "GitHub repository is not specified"
- **AND** the upload SHALL NOT be attempted
