# HagiCode Release

[![last commit](https://img.shields.io/github/last-commit/HagiCode-org/releases)](https://github.com/HagiCode-org/releases/commits/main)
[![website](https://img.shields.io/website?url=https%3A%2F%2Fhagicode.com&label=website)](https://hagicode.com)

[简体中文](./README_cn.md)

HagiCode Release is the automation hub for turning built artifacts into distributable releases, container images, and publish records.

## Product overview

This repository connects version discovery, GitHub Releases, and Docker Hub publishing so HagiCode builds can move from generated packages to public delivery.

## What this repository handles

- Monitor version sources and release only the newest unpublished version per run; older unpublished versions are reported as deferred backlog for later runs or manual handling
- Publish application packages to GitHub Releases
- Build and push multi-architecture Docker images
- Synchronize publish results and release metadata across delivery channels
- Ship the streamlined CLI baseline used inside the unified container runtime

## Common release commands

```bash
./build.sh VersionMonitor
./build.sh GitHubRelease --ReleaseVersion "1.2.3"
./build.sh DockerRelease --ReleaseVersion "1.2.3" --DockerPlatform "all"
```

Use repository-specific credentials and registry settings from [`ENVIRONMENT_VARIABLES.md`](./ENVIRONMENT_VARIABLES.md) when preparing a real release. The root wrappers run `python -m pybuild.entry`; direct Invoke remains available with `python -m invoke -c tasks <task>`.

## Local container build and test

The repository also ships a local `docker compose` workflow that reuses the PyBuild/Invoke build-context generation instead of bypassing it:

```bash
cp .env.local.example .env.local
cp .env.secrets.local.example .env.secrets.local
./scripts/docker-local-build.sh
./scripts/docker-local-up.sh
./scripts/docker-local-test.sh
./scripts/docker-local-logs.sh
./scripts/docker-local-down.sh
```

- `docker-compose.local.yml` uses the local image tag from `HAGICODE_LOCAL_IMAGE` and publishes the app to `127.0.0.1:5000` by default
- Local persistence stays under `./.local/hagicode/data` and `./.local/hagicode/saves`
- Keep plaintext local-only credentials in `.env.secrets.local`; the local scripts load it after `.env.local`, and `build.sh`/`build.ps1` also load it automatically outside GitHub Actions
- Local image builds still need outbound access to Docker Hub, `dot.net`, GitHub, and npm unless your machine already has equivalent mirrors or caches
- `scripts/docker-local-test.sh` waits for HTTP readiness and then smoke-tests the HagiScript-synced runtime baseline: `hagiscript`, `claude`, `openspec`, `skills`, `opencode`, and `codex` inside the running container

## Container CLI contract

The unified runtime image now builds from a clean `debian:bookworm-slim` base instead of inheriting the official `node` image user model. Node.js 24 is installed through an image-managed NVM layout under `/usr/local/nvm`, while npm-installed CLIs remain installed under `/home/hagicode/.npm-global`.
During image build, the Node bootstrap layer clears `NPM_CONFIG_PREFIX` before `nvm install`; after the image switches to `hagicode`, `npm config set prefix '/home/hagicode/.npm-global'` restores the runtime/global-install contract for npm-delivered CLIs.
The image then installs pinned `@hagicode/hagiscript` first and runs `hagiscript npm-sync --managed-runtime /home/hagicode/.hagiscript/node-runtime --manifest /app/bootstrap/hagiscript-sync-manifest.json` for the rest of the baked dependency baseline. The release-owned manifest selects the optional built-in agent CLIs `claude-code`, `fission-openspec`, `opencode`, and `codex`, while `skills` stays in the retained bundled tool baseline.
The managed HagiScript runtime is on `PATH`, so synced commands are available without runtime reinstall work in the entrypoint.

Only `hagicode` is supported as the non-root runtime user. When `PUID` and `PGID` are provided, container startup remaps that single user and reconciles ownership for `/home/hagicode`, its `.claude` state, and `/app`.

The unified runtime image bakes only the primary agent CLI baseline:

- `claude`
- `opencode`
- `codex`

`openspec` remains in the image as the retained workflow tool for spec-driven changes, and `skills` remains bundled as the retained skill-management CLI. Both are synchronized through the same HagiScript catalog-backed baseline and documented separately from the primary agent CLI baseline so provider scope does not expand again by accident.

The Docker entrypoint verifies the retained HagiScript-synced CLI baseline, resolves the HagiCode application entrypoint, applies Claude runtime configuration, and starts the app directly. `omniroute` and `code-server` are no longer treated as bundled release-image support, and the runtime no longer depends on `pm2` to supervise a multi-process startup chain.

Provider CLIs such as `copilot`, `codebuddy`, and `qodercli` now follow the HagiCode UI-managed install path instead of shipping in the container by default. `uipro` is no longer part of the image because the bundled `skills` command replaces its previous shipped-runtime workflow.

## Runtime startup and persistence

Container startup is now single-process from the release image perspective: the entrypoint prepares runtime prerequisites and then launches the detected HagiCode app assembly directly with `dotnet`.

- Both persistence roots are required in production deployments: `hagicode_data:/app/data` keeps system-scoped assets writable, and `hagicode_saves:/app/saves` keeps save-scoped runtime state writable
- Save-scoped HagiCode runtime state persists through `hagicode_saves:/app/saves`, with the active save rooted at `/app/saves/save0/...`
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

HagiCode Release takes outputs produced by the HagiCode core and desktop repositories, then publishes them to GitHub Releases, Docker Hub, and related active delivery channels. The historical Aliyun ACR personal edition configuration and target aliases remain for auditability but are disabled.
