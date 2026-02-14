# Change: Add Container Environment Variables Documentation

## Why

The Hagicode Release project uses Docker containerization with support for multiple container registries. While the project supports numerous environment variables for configuration (as evidenced by `.env.example`), there is no centralized documentation explaining these variables. Users currently need to examine `.env.example`, GitHub Actions workflows, and Nuke build scripts to understand all supported environment variables, creating a poor onboarding experience and making maintenance difficult.

## What Changes

- **ADDED** `docs/container-environment-variables.md` - A comprehensive documentation file covering:
  - Required environment variables for all container registries (Docker Hub, Azure ACR, Aliyun ACR)
  - Optional environment variables for build customization
  - Docker container-specific environment variables (ZAI API models, Agent Teams feature)
  - Usage examples for local development, Docker Compose, and Kubernetes deployments

- **UPDATED** `README.md` - Add reference link to the new environment variables documentation

## Impact

### Affected Specs
- `documentation` - New capability for container environment configuration reference

### Affected Code
- `docs/container-environment-variables.md` (NEW)
- `README.md` (MODIFIED - add documentation link)

### User Impact
- **Improved discoverability**: Users can find all environment variable options in a single document
- **Reduced onboarding time**: New users don't need to examine multiple files to configure the container
- **Better maintainability**: Centralized documentation makes it easier to keep docs in sync with code changes

## Success Criteria

1. Documentation file is created at `docs/container-environment-variables.md`
2. All environment variables from `.env.example` are documented with descriptions and default values
3. README.md includes a link to the new documentation
4. Documentation follows the project's markdown conventions
