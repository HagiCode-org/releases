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

The unified runtime image now builds from a clean `debian:bookworm-slim` base instead of inheriting the official `node` image user model. Node.js 24 is installed through an image-managed NVM layout under `/usr/local/nvm`, while baked CLIs remain installed under `/home/hagicode/.npm-global`.

Only `hagicode` is supported as the non-root runtime user. When `PUID` and `PGID` are provided, container startup remaps that single user and reconciles ownership for `/home/hagicode`, its `.claude` state, and `/app`.

The unified runtime image bakes only the primary agent CLI baseline:

- `claude`
- `opencode`
- `codex`

`openspec` remains in the image as the retained workflow tool for spec-driven changes, but it is documented separately from the primary agent CLI baseline so provider scope does not expand again by accident.

Provider CLIs such as `copilot`, `codebuddy`, and `qodercli` now follow the HagiCode UI-managed install path instead of shipping in the container by default. `uipro` is no longer part of the image because skill management replaces its previous shipped-runtime workflow.

## Bundled Code Server runtime

The unified image now bakes a pinned `code-server` binary into the same runtime baseline so Builder can export browser-IDE defaults without asking operators to install extra packages after startup.

- Builder `full-custom` mode exports `VsCodeServer__*` defaults directly into compose when you keep code-server enabled
- Dedicated host publishing remains opt-in, and the generated mapping binds to `127.0.0.1` by default for the first exposure step
- Password auth requires `CODE_SERVER_PASSWORD` or `CODE_SERVER_HASHED_PASSWORD`; the entrypoint bridges those variables to the standard `PASSWORD` / `HASHED_PASSWORD` names before app startup
- Runtime state still persists through the shared `hagicode_data:/app/data` volume, so there is no second mandatory Code Server data volume

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
