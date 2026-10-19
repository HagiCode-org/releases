# AGENTS.md - HagiCode Release Repository

This document describes the AI agents (Claude, OpenSpec, Codex, Copilot) that work with this repository.

## Overview

The HagiCode release repository manages automated releases of the HagiCode platform. This repository integrates with multiple AI agents to streamline release workflows and spec-driven development.

## Supported AI Agents

### Baked CLIs

All are installed into the unified image at build time through `hagiscript npm-sync` (see Docker Integration), not through separate base images.

- **Claude Code CLI** (`claude`): AI-assisted development; configured by the entrypoint (see below).
- **OpenSpec CLI** (`openspec`): spec-driven workflow tooling (proposals, changes, task lists); retained as workflow tooling, not a provider-facing agent CLI.
- **Skills CLI** (`skills`): version pinned through `skills`; retained as the bundled skill-management tool, not part of the primary provider-facing agent CLI baseline.
- **Codex CLI** (`codex`): AI coding task execution; latest major stream via `@openai/codex`; endpoint and key can be overridden through container runtime environment variables.

### Primary Agent CLI Baseline

The unified release image keeps only the primary agent CLI baseline baked into the container:

- `claude`
- `opencode`
- `codex`

`openspec` is still retained in the image as workflow tooling, and `skills` is retained as bundled skill-management tooling. They are documented separately from the primary provider-facing agent CLI baseline.

### UI-managed Provider CLIs

The unified release image no longer pre-installs provider CLIs that HagiCode can provision through the hero/system UI. Those providers now follow the product-managed install flow instead of the baked container baseline.

#### Copilot CLI

**Purpose**: GitHub Copilot coding agent workflow in terminal
**Installation**: Installed through the HagiCode UI when needed

Runtime notes:
- `COPILOT_BASE_URL` and `COPILOT_API_KEY` still configure runtime connectivity after the UI installs the CLI.

#### CodeBuddy CLI

**Purpose**: CodeBuddy ACP runtime for provider-driven coding workflows
**Installation**: Installed through the HagiCode UI when needed

Runtime notes:
- `CODEBUDDY_API_KEY` and `CODEBUDDY_INTERNET_ENVIRONMENT` still apply after the UI installs the CLI.

#### Qoder CLI

**Purpose**: ACP-compatible Qoder runtime for shared CLI toolchain workflows
**Installation**: Installed through the HagiCode UI when needed

Runtime notes:
- `QODER_PERSONAL_ACCESS_TOKEN` remains available for non-interactive container authentication after the UI installs the CLI.
- Container guidance still assumes ACP bootstrap via `qodercli --acp` once installed.

## Docker Integration

The unified Docker image pre-installs the retained workflow + runtime baseline:

- Primary agent CLIs, `openspec` and `skills` as listed under Primary Agent CLI Baseline
- Runtime SSH client: `openssh-client`

Container foundation notes:
- Base stages start from `debian:bookworm-slim`, not the official `node` image variants.
- Node.js 24 is installed through a shared NVM layout rooted at `/usr/local/nvm`.
- The Node bootstrap layer clears `NPM_CONFIG_PREFIX` before `nvm install`, then reapplies `/home/hagicode/.npm-global` later for the `hagicode` user.
- The image installs pinned `@hagicode/hagiscript` first, then uses `hagiscript npm-sync` with `/app/bootstrap/hagiscript-sync-manifest.json` to synchronize the retained baked baseline: `claude`, `openspec`, `skills`, `opencode`, and `codex`.
- `/home/hagicode/.npm-global` remains the HagiScript bootstrap prefix, `/home/hagicode/.hagiscript/node-runtime/bin` exposes the synced toolchain, and `hagicode` is the only supported non-root runtime user.

- `uipro` no longer ships because the bundled `skills` command replaces its previous runtime role.
- **Images**: `docker_deployment/Dockerfile.template` is a single multi-stage build. The `base` stage (Debian, Node.js 24, .NET 10 runtime, CLI baseline) is internal and is not pushed as an independent image; the `final` stage adds the application packages and is pushed as `hagicode:<version>`.

### AI Agent Configuration

The Docker entrypoint script (`docker-entrypoint.sh`) automatically configures AI agents based on environment variables:

In the entrypoint itself only Claude config, SSH bootstrap, Qoder token detection and the HagiScript toolchain check are implemented; the Codex, Copilot and CodeBuddy variables below are passed through to the container runtime. Full variable reference: `ENVIRONMENT_VARIABLES.md`.

#### Claude Code Configuration

- `ANTHROPIC_AUTH_TOKEN`: Anthropic API token (highest priority)
- `ANTHROPIC_URL`: Custom Anthropic API endpoint
- `ANTHROPIC_SONNET_MODEL`: Default Sonnet model
- `ANTHROPIC_OPUS_MODEL`: Default Opus model
- `ANTHROPIC_HAIKU_MODEL`: Default Haiku model
- `CLAUDE_HOST_CONFIG_ENABLED`: Enable/disable host config mount (default: true)
- `CLAUDE_CONFIG_MOUNT_PATH`: Path for mounted Claude config (default: /claude-mount)
- `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS`: Enable Agent Teams feature (default: 1)

#### Codex Global Settings Configuration

- `CODEX_BASE_URL`: Primary Codex endpoint variable
- `CODEX_API_KEY`: Primary Codex API key variable
- `OPENAI_BASE_URL`: Compatibility alias for endpoint
- `OPENAI_API_KEY`: Compatibility alias for API key

Precedence:
- Base URL: `CODEX_BASE_URL` > `OPENAI_BASE_URL`
- API key: `CODEX_API_KEY` > `OPENAI_API_KEY`

#### Copilot Global Settings Configuration

- `COPILOT_BASE_URL`: Copilot endpoint variable
- `COPILOT_API_KEY`: Copilot API key variable

Copilot variables are isolated and do not override Codex/OpenAI variables. They apply after Copilot has been installed through the product UI.

#### Qoder Runtime Configuration

- `QODER_PERSONAL_ACCESS_TOKEN`: Non-interactive qoder authentication token passed through to container runtime

Startup logs never print the raw `QODER_PERSONAL_ACCESS_TOKEN` value. Install path and ACP bootstrap are covered under UI-managed Provider CLIs above.

#### Host Configuration

Claude Code can be configured using host-mounted config files:

```bash
docker run -v ~/claude-config:/claude-mount hagicode/hagicode
```

The container will automatically copy settings from `/claude-mount/settings.json` to the hagicode user's `.claude` directory.

#### SSH Bootstrap Configuration

The Docker entrypoint also supports startup-time SSH bootstrap for mounted secrets:

- `SSH_PRIVATE_KEY_PATH`: required to enable SSH bootstrap; unset means the entrypoint skips SSH preparation
- `SSH_KNOWN_HOSTS_PATH`: optional readable file copied into `/home/hagicode/.ssh/known_hosts`
- `SSH_STRICT_HOST_KEY_CHECKING`: optional override for the generated SSH config; defaults to `accept-new`

Runtime behavior:
- The entrypoint copies the mounted private key into `/home/hagicode/.ssh/imported_key`
- It writes deterministic SSH config to `/home/hagicode/.ssh/config`, exports `GIT_SSH_COMMAND`, and constrains the runtime to `IdentitiesOnly yes`
- `.ssh` is permission-hardened (`700` directory, `600` key, `644` known_hosts/config`) and all generated files stay owned by `hagicode`
- If an explicitly configured SSH path is missing, unreadable, or not a file, startup fails fast with path-level diagnostics and never prints secret contents

## Build System Integration

The release repository uses the PyBuild/Invoke engine under `pybuild/`. Root wrappers keep the legacy target-first contract while routing to Python:

```bash
./build.sh VersionMonitor
./build.sh GitHubRelease --ReleaseVersion "1.2.3"
./build.sh DockerRelease --ReleaseVersion "1.2.3" --DockerPlatform "all"
python -m invoke -c tasks version-monitor
```

`build.sh`, `build.ps1`, and `build.cmd` execute `python -m pybuild.entry`. Requests for `--engine nuke` or `BUILD_ENGINE=nuke` must fail before target side effects; Nuke orchestration has been removed for this repository.

### Repository Structure

- `pybuild/entry.py` - wrapper compatibility parser for legacy targets and `--target` forms
- `pybuild/tasks.py` - Invoke task collection for release targets
- `pybuild/targets.py` - legacy target name to Invoke task alias map (add new targets here and in `tasks.py`)
- `pybuild/config.py` - build configuration resolution (`build-config.yaml`, canonical and legacy environment names)
- `pybuild/runtime.py` - subprocess and command-execution helpers
- `pybuild/release_source.py` - release package `index.json` parsing and zip download helpers
- `pybuild/versioning.py` - semantic version sorting and version-monitor planning
- `pybuild/github_ops.py` - GitHub Release and repository dispatch helpers
- `pybuild/docker_ops.py` - Docker context generation, login, and buildx push helpers
- `build-config.yaml` - default Docker and registry build settings
- `tasks.py` - root Invoke shim exposing `pybuild.tasks.ns`
- `docker_deployment/` - `Dockerfile.template` (pinned HagiScript bootstrap version in its header), `docker-entrypoint.sh`, and `hagiscript-sync-manifest.json` (agent CLI IDs synced at image build)
- `.github/workflows/` - `version-monitor.yml`, `github-release-workflow.yml`, `docker-build-dockerhub.yml`, and the disabled `docker-build-aliyun-acr.yml`; all set up Python 3.12 (`pyproject.toml` allows 3.11 to 3.13, pinned `invoke` and `PyYAML` in `requirements.lock.txt`)
- `tests/test_pybuild.py` - PyBuild unit tests

### Supported Targets

Legacy wrapper target names map to Invoke tasks:

| Legacy target | Invoke task | Purpose |
|---|---|---|
| `VersionMonitor` | `version-monitor` | Read package source versions, compare GitHub Releases, dispatch release workflows |
| `GitHubRelease` | `github-release` | Download packages when needed and create/update GitHub Release assets |
| `DockerRelease` | `docker-release` | Build/push DockerHub image path by default |
| `PushToAliyunAcr` | `push-to-aliyun-acr` | Historical disabled alias; exits before Aliyun ACR publishing |
| `PushToDockerHub` | `push-to-dockerhub` | Build/push DockerHub image |
| `Download` | `download` | Download zip packages from package source |
| `DockerPrepareLocalContext` | `docker-prepare-local-context` | Prepare `output/docker-build-context` for local Docker Compose builds |
| `ConfigurationValidate` | `configuration-validate` | Validate build configuration |
| `DetermineBuildConfig` | `determine-build-config` | Resolve release version and stable/prerelease status |

### Parameters and Environment

Prefer canonical environment names such as `RELEASE_VERSION`, `DOCKER_PLATFORM`, `RELEASE_PACKAGE_INDEX_URL`, `RELEASE_PACKAGE_BASE_URL`, `GITHUB_TOKEN`, `GITHUB_REPOSITORY`, and `DOCKERHUB_*`.

Backward-compatible `NUGEX_*` aliases remain accepted for existing workflows and scripts. CLI parameters also accept legacy PascalCase forms, for example `--ReleaseVersion` and `--DockerPlatform`.

The historical `ALIYUN_ACR_*` and `NUGEX_AliyunAcr*` aliases remain in PyBuild for auditability, but Aliyun ACR personal edition publishing is disabled. Active Docker Hub execution does not require or read them.

### Local Docker Workflow

The local scripts under `scripts/` continue to call the root wrapper:

1. `scripts/docker-local-build.sh` reuses matching packages already present in `output/download`; it does not download on its own.
2. It then calls `DockerPrepareLocalContext`.
3. It builds `output/docker-build-context` with `docker buildx build --load` for local Compose use.

Run the built image with `docker-compose.local.yml` through `scripts/docker-local-up.sh`, then `docker-local-test.sh` (readiness and CLI baseline smoke test), `docker-local-logs.sh` and `docker-local-down.sh`. Copy `.env.local.example` to `.env.local` and `.env.secrets.local.example` to `.env.secrets.local` (both untracked; keep plaintext credentials only in the secrets file).

Keep generated packages under `output/download` and generated Docker context under `output/docker-build-context`.

### CI/CD

GitHub Actions install Python, then call the root wrapper with legacy target names. Do not add .NET/Nuke setup steps for release orchestration.

### Testing

Use focused Python tests for PyBuild behavior:

```bash
python -m pytest tests
```

For smoke checks without side effects, use dry-run paths:

```bash
./build.sh PushToDockerHub --ReleaseVersion "1.2.3" --DockerPlatform "linux-amd64" --DryRun true
```
