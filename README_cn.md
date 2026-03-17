# HagiCode Release

[English](./README.md)

HagiCode Release 是把构建产物转换为可分发版本、容器镜像和发布记录的自动化中枢。

## 产品概览

本仓库把版本发现、GitHub Release 与多 Registry Docker 发布串成一条链路，让 HagiCode 的构建结果可以从生成包走向公开交付。

## 本仓库负责什么

- 监控版本来源并决定何时触发发布流水线
- 将应用程序包发布到 GitHub Releases
- 构建并推送多架构 Docker 镜像
- 在多条交付通道之间同步发布结果与版本元数据
- 提供统一容器运行时内置的 CLI 基线

## 主要目录

- `nukeBuild/` - 发布自动化目标与共享构建逻辑
- `.github/workflows/` - 监控与发布相关的 CI/CD 流水线
- `docker_deployment/` - 容器构建上下文、Dockerfile 与入口脚本
- `output/` - 本地发布过程生成的产物
- `ENVIRONMENT_VARIABLES.md` - 运行时与发布配置说明

## 常用发布命令

```bash
./build.sh VersionMonitor
./build.sh GitHubRelease --ReleaseVersion "1.2.3"
./build.sh DockerRelease --ReleaseVersion "1.2.3" --DockerPlatform "all"
```

准备真实发布时，请结合 `ENVIRONMENT_VARIABLES.md` 配置对应的凭据与 Registry 参数。

## 在生态中的角色

HagiCode Release 接收 `repos/hagicode-core`、`repos/hagicode-desktop` 等仓库生成的构建产物，并把它们发布到 GitHub Releases、Azure ACR、阿里云 ACR、DockerHub 等交付渠道。
