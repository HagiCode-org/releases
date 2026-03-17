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
- Ship the CLI baseline used inside the unified container runtime

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

## Ecosystem role

HagiCode Release takes outputs produced by repositories such as `repos/hagicode-core` and `repos/hagicode-desktop`, then publishes them to GitHub Releases, Azure ACR, Aliyun ACR, DockerHub, and related delivery channels.
