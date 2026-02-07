# Spec Delta: CI/CD Build and Release

## ADDED Requirements

### Requirement: Container Image Build
The system SHALL provide automated container image building using GitHub Actions.

#### Scenario: Successful container build on tag push
- **WHEN** a git tag matching pattern `v*.*.*` is pushed
- **THEN** the GitHub Actions workflow triggers automatically
- **AND** builds a Docker image using the application Dockerfile
- **AND** tags the image with the full version number

#### Scenario: Multi-tag version strategy
- **WHEN** a version `v1.2.3` is released
- **THEN** the Docker image is tagged with `v1.2.3`, `v1.2`, `v1`, and `latest`
- **AND** all tags are pushed to Docker Hub

### Requirement: Docker Hub Publishing
The system SHALL publish container images to Docker Hub registry.

#### Scenario: Successful image push
- **WHEN** the container image build completes successfully
- **THEN** the system authenticates to Docker Hub using configured credentials
- **AND** pushes all version tags to the `newbe36524` organization
- **AND** verifies the push succeeded

#### Scenario: Authentication failure handling
- **WHEN** Docker Hub authentication fails
- **THEN** the workflow fails with clear error message
- **AND** no partial images are published

### Requirement: GitHub Release Automation
The system SHALL automatically create GitHub releases on version tags.

#### Scenario: Release creation on tag push
- **WHEN** a version tag is pushed
- **THEN** a GitHub Release is created with the version as title
- **AND** release notes are auto-generated from commit messages
- **AND** build artifacts are uploaded to the release

#### Scenario: Pre-release detection
- **WHEN** a version tag contains `-alpha`, `-beta`, or `-rc`
- **THEN** the GitHub Release is marked as pre-release
- **AND** the `latest` Docker tag is NOT updated

### Requirement: Cross-Platform Binary Build
The system SHALL build standalone executables for multiple platforms.

#### Scenario: Linux build
- **WHEN** the build workflow is triggered
- **THEN** a self-contained linux-x64 binary is built
- **AND** the binary is packaged as a downloadable artifact

#### Scenario: Windows build
- **WHEN** the build workflow is triggered
- **THEN** a self-contained win-x64 binary is built
- **AND** the binary is packaged as a downloadable artifact

#### Scenario: macOS build
- **WHEN** the build workflow is triggered
- **THEN** a self-contained osx-x64 binary is built
- **AND** the binary is packaged as a downloadable artifact

### Requirement: Build Configuration
The system SHALL support configurable build options via YAML configuration.

#### Scenario: Framework-dependent build mode
- **WHEN** `FRAMEWORK_DEPENDENT` build mode is selected
- **THEN** the application is built without .NET runtime included
- **AND** the resulting artifact size is minimized
- **AND** the Docker image uses the base image with runtime

#### Scenario: Self-contained build mode
- **WHEN** `SELF_CONTAINED` build mode is selected
- **THEN** the application includes .NET runtime
- **AND** the binary can run without .NET installed
- **AND** the artifact size is larger

### Requirement: Health Check Integration
The system SHALL provide container health checks.

#### Scenario: HTTP health check
- **WHEN** the container is running
- **THEN** an HTTP endpoint on port 5000 responds to health checks
- **AND** the docker-compose health check passes
- **AND** unhealthy containers are automatically restarted

### Requirement: Environment Configuration
The system SHALL support environment-specific configurations.

#### Scenario: Development environment
- **WHEN** the application runs in development mode
- **THEN** environment variables are loaded from `.env` file
- **AND** local database connections are configured
- **AND** debug logging is enabled

#### Scenario: Production environment
- **WHEN** the application runs in production mode
- **THEN** environment variables are loaded from `.env.docker` file
- **AND** production database connections are configured
- **AND** Application Insights telemetry is enabled
