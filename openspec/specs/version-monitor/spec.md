# Version Monitor Specification

## Overview

The Version Monitor feature automatically monitors Azure Blob Storage for new versions of the application and triggers GitHub releases for versions that haven't been published yet.

## Requirements

### Functional Requirements

1. **Azure Version Detection**: The system must download and parse `index.json` from Azure Blob Storage to extract all available versions.

2. **GitHub Release Comparison**: The system must retrieve all existing GitHub releases and compare them with Azure versions.

3. **New Version Identification**: The system must identify versions that exist in Azure but not in GitHub releases (with or without 'v' prefix).

4. **Release Triggering**: When new versions are detected, the system must trigger the release workflow using GitHub `repository_dispatch` events.

5. **Dry Run Mode**: The system must support a dry run mode that performs all checks without actually triggering releases.

6. **Scheduled Execution**: The system must run on a schedule (every 4 hours) and support manual triggering.

7. **Error Handling**: The system must create a GitHub issue when execution fails.

## Success Scenarios

### Scenario 1: New Versions Detected and Release Triggered

**Given** Azure Blob Storage contains versions [1.0.0, 1.1.0, 1.2.0]
**And** GitHub releases contain [1.0.0, 1.1.0]
**When** the Version Monitor runs
**Then** version 1.2.0 should be identified as new
**And** a repository_dispatch event should be triggered for version 1.2.0
**And** the event payload should contain `{"version": "1.2.0"}`

### Scenario 2: No New Versions

**Given** Azure Blob Storage contains versions [1.0.0, 1.1.0, 1.2.0]
**And** GitHub releases contain [1.0.0, 1.1.0, 1.2.0]
**When** the Version Monitor runs
**Then** no new versions should be identified
**And** no repository_dispatch events should be triggered
**And** a log message should indicate all versions are already released

### Scenario 3: Dry Run Mode with New Versions

**Given** Azure Blob Storage contains versions [1.0.0, 1.1.0, 1.2.0]
**And** GitHub releases contain [1.0.0, 1.1.0]
**And** DryRun is set to true
**When** the Version Monitor runs
**Then** version 1.2.0 should be identified as new
**And** no repository_dispatch events should be triggered
**And** a log message should indicate "[DRY RUN] Would trigger release for 1.2.0"

### Scenario 4: Version Comparison with 'v' Prefix

**Given** Azure Blob Storage contains versions [1.0.0, 1.1.0]
**And** GitHub releases contain [v1.0.0, v1.1.0]
**When** the Version Monitor runs
**Then** no new versions should be identified
**And** versions should be compared case-insensitively

## Error Scenarios

### Scenario 5: Azure Access Failure

**Given** the Azure Blob Storage SAS URL is invalid or expired
**When** the Version Monitor runs
**Then** the system should log an error "Failed to download index.json from Azure"
**And** the workflow should fail
**And** a GitHub issue should be created with title "Version Monitor Failed - [timestamp]"

### Scenario 6: GitHub API Call Failure

**Given** the GitHub token is invalid or has insufficient permissions
**When** the Version Monitor attempts to retrieve releases
**Then** the system should log an error "Failed to get GitHub releases"
**And** the workflow should fail
**And** a GitHub issue should be created with title "Version Monitor Failed - [timestamp]"

### Scenario 7: Repository Dispatch Trigger Failure

**Given** a new version is detected
**And** the GitHub token has insufficient permissions for repository_dispatch
**When** the Version Monitor attempts to trigger the release
**Then** the system should log an error "Failed to trigger release for version [version]"
**And** the workflow should fail
**And** a GitHub issue should be created

## API Contracts

### Nuke Target: VersionMonitor

**Parameters:**
- `AzureBlobSasUrl` (required): SAS URL for Azure Blob Storage
- `GitHubToken` (required): GitHub authentication token
- `GitHubRepository` (required): Repository in format "owner/repo"
- `DryRun` (optional): Boolean flag for dry run mode (default: false)

**Exit Codes:**
- 0: Success (no errors, with or without new versions)
- Non-zero: Error occurred (Azure access failed, GitHub API failed, etc.)

### GitHub Repository Dispatch Event

**Event Type:** `version-monitor-release`

**Payload:**
```json
{
  "version": "1.2.0"
}
```

## Configuration

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `AZURE_BLOB_SAS_URL` | Yes | SAS URL for Azure Blob Storage with Read permissions |
| `GITHUB_TOKEN` | Yes | GitHub token with `repo` and `actions: write` permissions |
| `GITHUB_REPOSITORY` | Yes | Repository in format "owner/repo" |
| `DRY_RUN` | No | Set to "true" for dry run mode |

### GitHub Actions Permissions

The workflow requires:
- `contents: read` - to read repository information
- `actions: write` - to send repository_dispatch events

## Usage

### Local Testing

```bash
nuke VersionMonitor \
  --AzureBlobSasUrl "<sas-url>" \
  --GitHubToken "<token>" \
  --GitHubRepository "owner/repo" \
  --DryRun true
```

### GitHub Actions

The workflow runs automatically every 4 hours and can be manually triggered from the Actions tab with an optional dry_run parameter.
