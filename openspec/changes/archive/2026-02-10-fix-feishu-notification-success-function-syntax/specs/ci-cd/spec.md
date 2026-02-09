# CI/CD Specification

## ADDED Requirements

### Requirement: GitHub Actions Expression Syntax Compliance

All GitHub Actions workflow files MUST use valid expression syntax according to GitHub Actions documentation. Functions SHALL only be used in contexts where they are supported (e.g., `if` conditions). Context variables like `job.status` SHALL be used instead of unsupported functions in `with` parameter blocks.

#### Scenario: Workflow syntax validation passes

- **WHEN** a workflow file is committed to the repository
- **THEN** GitHub Actions MUST validate the syntax without errors
- **AND** no "Unrecognized function" errors SHALL occur
- **AND** expressions in `with` parameters MUST use `job.status` instead of `success()`

### Requirement: Feishu Release Status Notification

The system SHALL send Feishu notifications upon completion of the release workflow, regardless of success or failure status. The notification step MUST execute with `if: always()` to ensure notifications are sent even when the workflow fails.

#### Scenario: Successful release notification format

- **WHEN** the release workflow completes successfully (`job.status == 'success'`)
- **THEN** the Feishu notification title SHALL display "Hagicode Release 发布成功 ✅"
- **AND** the notification message SHALL include "**状态**: 发布成功"
- **AND** the notification SHALL include version number, trigger user, timestamp, and release links

#### Scenario: Failed release notification format

- **WHEN** the release workflow fails or is cancelled (`job.status != 'success'`)
- **THEN** the Feishu notification title SHALL display "Hagicode Release 发布失败 ❌"
- **AND** the notification message SHALL include "**状态**: 发布失败"
- **AND** the notification SHALL still include version number, trigger user, timestamp, and release links for debugging

#### Scenario: Notification always executes

- **WHEN** the release workflow completes in any state (success, failure, or cancelled)
- **THEN** the Feishu notification step MUST execute (configured with `if: always()`)
- **AND** the notification SHALL accurately reflect the actual job status using `job.status` context variable
