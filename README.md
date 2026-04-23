# HagiCode Release

[简体中文](./README_cn.md)

HagiCode Release is the automation hub for turning built artifacts into distributable releases, container images, and publish records.

## Product overview

This repository connects version discovery, GitHub Releases, and multi-registry Docker publishing so HagiCode builds can move from generated packages to public delivery.

## What this repository handles

- Monitor version sources and decide when a release pipeline should run
- Publish application packages to GitHub Releases
- Build and push multi-architecture Docker images
- Synchronize publish results and release metadata across delivery channels
- Ship the streamlined CLI baseline used inside the unified container runtime

## Main areas

- `nukeBuild/` - release automation targets and shared build logic
- `.github/workflows/` - CI/CD pipelines for monitoring and publishing
- `docker_deployment/` - container build context, Dockerfiles, and entrypoint scripts
- `output/` - generated artifacts during local release work
- `ENVIRONMENT_VARIABLES.md` - runtime and publishing configuration reference

## Common release commands

```bash
./build.sh VersionMonitor
./build.sh GitHubRelease --ReleaseVersion "1.2.3"
./build.sh DockerRelease --ReleaseVersion "1.2.3" --DockerPlatform "all"
```

Use repository-specific credentials and registry settings from `ENVIRONMENT_VARIABLES.md` when preparing a real release.

## Steam Linux desktop artifact verification

When a Linux desktop package bundles the optional portable payload under `resources/extra/portable-fixed/current`, keep the Steam startup contract aligned with the desktop bootstrap fix:

- Steam launch of the packaged artifact must log that Steam Linux compatibility mode was enabled before the first window is created
- The same packaged artifact launched directly from the CLI must log that compatibility mode was skipped and direct CLI launch keeps the default graphics path
- If later startup diagnostics are captured, the copied startup-failure log should still begin with the `[StartupCompatibility]` context line so release triage can separate graphics-mode handling from unrelated failures

## Trigger boundaries

Automatic publishing now has a single entry point:

- `./build.sh VersionMonitor` still discovers every unpublished Azure version, but it auto-selects only the newest unpublished version for the current run
- GitHub Release automation starts only from `repository_dispatch` with event type `version-monitor-release`
- Docker automation starts only from `repository_dispatch` with registry-specific event types (`version-monitor-docker-aliyun`, `version-monitor-docker-azure`, `version-monitor-docker-dockerhub`)
- Older unpublished versions are reported as deferred backlog for later scheduled runs or manual handling

Manual reruns stay available, but they are explicit:

- `github-release-workflow.yml` requires `workflow_dispatch.version`
- Each `docker-build-*.yml` workflow requires `workflow_dispatch.version` and keeps optional `platform` / `dry_run`
- Creating or reusing a Git tag no longer auto-starts GitHub Release or Docker workflows

## Container CLI contract

The unified runtime image now builds from a clean `debian:bookworm-slim` base instead of inheriting the official `node` image user model. Node.js 22 is installed through an image-managed NVM layout under `/usr/local/nvm`, while npm-installed CLIs remain installed under `/home/hagicode/.npm-global`.
During image build, the Node bootstrap layer clears `NPM_CONFIG_PREFIX` before `nvm install`; after the image switches to `hagicode`, `npm config set prefix '/home/hagicode/.npm-global'` restores the runtime/global-install contract for npm-delivered CLIs.
`code-server` is installed from the pinned standalone release archive and linked on `PATH`, so it does not depend on the npm global prefix.

Only `hagicode` is supported as the non-root runtime user. When `PUID` and `PGID` are provided, container startup remaps that single user and reconciles ownership for `/home/hagicode`, its `.claude` state, and `/app`.

The unified runtime image bakes only the primary agent CLI baseline:

- `claude`
- `opencode`
- `codex`

`openspec` remains in the image as the retained workflow tool for spec-driven changes, but it is documented separately from the primary agent CLI baseline so provider scope does not expand again by accident.

Provider CLIs such as `copilot`, `codebuddy`, and `qodercli` now follow the HagiCode UI-managed install path instead of shipping in the container by default. `uipro` is no longer part of the image because skill management replaces its previous shipped-runtime workflow.

## Omniroute unified provider bootstrap

The release image now treats Omniroute as the unified local provider proxy for Claude, Codex/OpenAI, and OpenCode traffic inside the container.

- Default local bind: `127.0.0.1:4060`
- Local management/runtime state: `/app/data/omniroute`
- Shared process supervision: `pm2-runtime`
- App readiness gate: `/app/data/omniroute/runtime/hagicode.ready`

Startup order is intentionally Omniroute-first:

1. The entrypoint resolves the HagiCode app command and captures upstream provider credentials before any local reroute happens.
2. The entrypoint normalizes Omniroute runtime paths and persisted secrets under `/app/data/omniroute`.
3. The entrypoint exports the local Omniroute endpoint back into the runtime environment consumed by `claude`, `codex`, `opencode`, and HagiCode.
4. `pm2-runtime` starts two managed processes: `omniroute` and `hagicode-app`.
5. `hagicode-app` starts through `wait-for-ready.sh`, which blocks on the ready file until Omniroute bootstrap succeeds.
6. The bootstrap helper logs into local Omniroute, upserts provider nodes/connections through the Omniroute HTTP API, writes bootstrap state, and releases the ready file.

Bootstrap is API-first and idempotent. The container does not mutate Omniroute SQLite files directly; it reuses persisted state and upserts only the providers that have the minimum upstream credentials configured.

After bootstrap, the container runtime rewires the main provider endpoints to local Omniroute URLs:

- `ANTHROPIC_URL` points to the local Omniroute API base URL and `ANTHROPIC_AUTH_TOKEN` is replaced with the shared local key for Claude CLI traffic.
- `CODEX_BASE_URL` / `OPENAI_BASE_URL` point to the local Omniroute API base URL and `CODEX_API_KEY` / `OPENAI_API_KEY` are replaced with the shared local key.
- `OPENCODE_BASE_URL` / `OPENCODE_API_BASE_URL` point to the local Omniroute API base URL and `OPENCODE_API_KEY` is replaced with the shared local key.
- HagiCode receives `HAGICODE_OMNIROUTE_ENABLED=true`, `HAGICODE_OMNIROUTE_BASE_URL`, `HAGICODE_OMNIROUTE_API_BASE_URL`, `OmniRoute__Enabled`, `OmniRoute__BaseUrl`, and `OmniRoute__ApiBaseUrl`.

## Bundled Code Server runtime

The unified image now bakes a pinned `code-server` binary into the same runtime baseline so Builder can export browser-IDE defaults without asking operators to install extra packages after startup.

- Builder `full-custom` mode exports `VsCodeServer__*` defaults directly into compose when you keep code-server enabled
- Builder now exposes a shared EULA toggle that exports `ACCEPT_EULA=Y` only when operators explicitly opt in, and the entrypoint refuses startup without an accepted value
- Dedicated host publishing remains opt-in, and the generated mapping binds to `127.0.0.1` by default for the first exposure step
- Password auth requires `CODE_SERVER_PASSWORD` or `CODE_SERVER_HASHED_PASSWORD`; the entrypoint bridges those variables to the standard `PASSWORD` / `HASHED_PASSWORD` names before app startup
- Both persistence roots are required in production deployments: `hagicode_data:/app/data` keeps system-scoped assets writable, and `hagicode_saves:/app/saves` keeps save-scoped runtime state writable
- System-scoped assets still persist through `hagicode_data:/app/data`, and managed Code Server data stays under `/app/data/code-server`
- Save-scoped HagiCode runtime state now persists through `hagicode_saves:/app/saves`, with the active save rooted at `/app/saves/save0/...`
- The image and entrypoint prepare only `/app/data` and `/app/saves`; the application runtime still initializes `/app/saves/save0/config` and `/app/saves/save0/data` on demand
- If you are upgrading from an older single-volume deployment, add a named volume or bind mount for `/app/saves` before replacing the container

Minimal mount layout:

```yaml
volumes:
  - hagicode_data:/app/data
  - hagicode_saves:/app/saves
```

## Startup SSH bootstrap

The release image now installs `openssh-client` and can import a mounted private key during startup when SSH access is explicitly required.

- Set `SSH_PRIVATE_KEY_PATH` to a mounted private key file to enable bootstrap
- Optionally set `SSH_KNOWN_HOSTS_PATH` to import a mounted `known_hosts` file
- Optionally set `SSH_STRICT_HOST_KEY_CHECKING` to override the documented default of `accept-new`
- Leave `SSH_PRIVATE_KEY_PATH` unset to skip SSH bootstrap entirely

At startup the entrypoint copies the mounted key into `/home/hagicode/.ssh/imported_key`, writes deterministic SSH config at `/home/hagicode/.ssh/config`, fixes ownership for the `hagicode` runtime user, and exports `GIT_SSH_COMMAND` so downstream `git` and `ssh` commands use the imported identity.

If `SSH_PRIVATE_KEY_PATH` is set but the file is missing, unreadable, or not a regular file, container startup fails fast with path-level diagnostics and never prints secret contents.

## Ecosystem role

HagiCode Release takes outputs produced by repositories such as `repos/hagicode-core` and `repos/hagicode-desktop`, then publishes them to GitHub Releases, Azure ACR, Aliyun ACR, DockerHub, and related delivery channels.
