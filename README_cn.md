# HagiCode 发布仓库

[English](./README.md)

HagiCode 平台的自动化发布管理。本仓库负责版本监控、GitHub Release 和多架构 Docker 镜像发布到 Edge ACR。

## 概述

HagiCode 发布仓库管理：

- **版本监控**：检测 Azure Blob Storage 中的新版本
- **GitHub Releases**：创建包含应用程序包的 GitHub Release
- **Docker 多架构构建**：为 linux/amd64 和 linux/arm64 构建和发布 Docker 镜像
- **多 Registry 发布**：将 Docker 镜像推送到 Azure ACR、阿里云 ACR 和 DockerHub
- **发布结果追踪**：自动生成并上传发布结果 JSON 文件到 GitHub Release
- **阿里云镜像同步**：从 Azure ACR 同步镜像到阿里云 ACR，支持自动版本发现
- **DockerHub 集成**：使用基于用户名的路径格式同步镜像到 DockerHub

## 快速开始

### 前置条件

- .NET 10 SDK
- Docker 20.10 或更高版本，需支持 buildx
- Azure Blob Storage 访问（SAS URL）
- GitHub 个人访问令牌（PAT）
- Edge ACR 凭据（用户名、密码、registry）
- jq（用于多架构验证中的 JSON 解析）

### 本地构建

```bash
# 克隆仓库
git clone https://github.com/your-org/hagicode-release.git
cd hagicode-release

# 运行 Nuke 构建（使用不带 v 前缀的版本）
./build.sh DockerRelease \
  --ReleaseVersion "1.2.3" \
  --AzureBlobSasUrl "https://..." \
  --AzureAcrRegistry "hagicode.azurecr.io" \
  --AzureAcrUsername "username" \
  --AzureAcrPassword "password" \
  --DockerPlatform "all"
```

### 版本格式要求

**重要**：版本号遵循语义化版本控制（semver）格式。

- **推荐**：`1.2.3`、`1.2.3-beta.1`（不带 v 前缀）
- **接受**：`v1.2.3`、`v1.2.3-beta.1`（带 v 前缀，为向后兼容）
- **错误**：`1.2`、`1.2.3 beta`、`latest`

**注意**：带和不带 v 前缀的版本号功能上相同（例如 `1.2.3` = `v1.2.3`）。系统将它们视为相同版本进行比较和下载。

版本格式在以下位置自动验证：
1. 版本监控器（跳过无效版本并发出警告）
2. Docker 构建工作流（失败并显示错误消息）

## 构建目标

### 可用目标

| 目标 | 描述 | 依赖 |
|--------|-------------|--------------|
| `Clean` | 清理输出目录 | - |
| `Restore` | 恢复构建依赖 | - |
| `Download` | 从 Azure Blob Storage 下载包 | - |
| `VersionMonitor` | 监控 Azure Blob 中的新版本 | - |
| `GitHubRelease` | 创建包含程序包的 GitHub release | Download |
| `DockerBuild` | 构建 Docker 镜像（仅本地） | Download |
| `DockerPush` | 将 Docker 镜像推送到配置的 registry | DockerBuild |
| `DockerRelease` | 构建并推送 Docker 镜像到配置的 registry | DockerPush |

### Docker 构建目标

#### 构建单架构（AMD64）

```bash
./build.sh DockerBuild \
  --ReleaseVersion "1.2.3" \
  --DockerPlatform "linux-amd64"
```

#### 构建单架构（ARM64）

```bash
./build.sh DockerBuild \
  --ReleaseVersion "1.2.3" \
  --DockerPlatform "linux-arm64"
```

#### 构建多架构（两者）

```bash
./build.sh DockerRelease \
  --ReleaseVersion "1.2.3" \
  --DockerPlatform "all"
```

## Docker 镜像

### 镜像结构

```
hagicode/hagicode:1.2.3         - 带版本标签的应用程序镜像
hagicode/hagicode:1.2            - 带次版本标签的应用程序镜像
hagicode/hagicode:1              - 带主版本标签的应用程序镜像
hagicode/hagicode:latest          - 带 latest 标签的应用程序镜像
```

**注意**：Docker 镜像使用统一的 multi-stage 构建。基础工具（Node.js、.NET 运行时、CLI 工具）和应用程序代码合并在单个镜像中。不向 registry 推送单独的基础镜像。

### 随附 CLI 支持矩阵

统一镜像将以下 CLI 工具 baked 到 `/home/hagicode/.npm-global/bin`，以便它们对 `hagicode` 用户可用，无需启动后安装：

| CLI | 镜像包基线 | 运行时合约 | 版本覆盖 |
|-----|------------------------|------------------|------------------|
| Claude Code | `@anthropic-ai/claude-code@2.1.71` | 标准 Claude Code CLI 工作流 | `CLAUDE_CODE_CLI_VERSION` |
| OpenSpec | `@fission-ai/openspec@1.2.0` | 容器内的 Spec 工作流命令 | `OPENSPEC_CLI_VERSION` |
| UIPro | `uipro-cli@2.2.3` | 技能安装 / UI 工作流支持 | `UIPRO_CLI_VERSION` |
| OpenCode | `opencode-ai@1.2.25` | 作为 `opencode` 命令发布；HagiCode 继续在 `AI:OpenCode` 中使用托管的 OpenCode 共享 HTTP 运行时基线 | 无（固定镜像基线）|
| Codex | `@openai/codex@0.112.0` | 终端编码工作流 | `CODEX_CLI_VERSION` |
| Copilot | `@github/copilot@1.0.2` | 终端 Copilot 工作流 | `COPILOT_CLI_VERSION` |
| CodeBuddy | `@tencent-ai/codebuddy-code@2.61.2` | HagiCode 启动 `codebuddy --acp`；运行时可能还需要 `CODEBUDDY_API_KEY` 和 `CODEBUDDY_INTERNET_ENVIRONMENT` | `CODEBUDDY_CLI_VERSION` |
| IFlow | `@iflow-ai/iflow-cli@0.5.17` | HagiCode 启动 `iflow --experimental-acp --port {port}`；CLI 登录或等效的挂载运行时状态必须在运行时仍然存在 | `IFLOW_CLI_VERSION` |

### 容器 CLI 冒烟测试

在构建或拉取镜像后，使用这些发布端检查来确认预期命令存在：

```bash
docker run --rm --entrypoint /bin/sh hagicode.azurecr.io/hagicode:1.2.3 -c '
  set -eu
  codebuddy --version
  iflow --version
  opencode --version
'
```

如果想一次验证完整的随附 CLI 集，扩展相同命令：

```bash
docker run --rm --entrypoint /bin/sh hagicode.azurecr.io/hagicode:1.2.3 -c '
  set -eu
  claude --version
  openspec --version
  uipro --version
  opencode --version
  codex --version
  copilot --version
  codebuddy --version
  iflow --version
'
```

### CodeBuddy、IFlow 和 OpenCode 运行时说明

- CodeBuddy 已安装在镜像中，但认证仍是运行时问题。如果使用 API 密钥模式，请同时传递 `CODEBUDDY_API_KEY` 和 `CODEBUDDY_INTERNET_ENVIRONMENT`（当前文档化值通常是 `ioa`）。
- IFlow 也安装在镜像中，但首次运行登录不会 baked 到容器中。在期望 HagiCode 成功启动 IFlow provider 之前，请以交互方式完成 `iflow` 登录或挂载预先存在的运行时状态。
- OpenCode 仍然是支持的镜像基线的一部分。镜像为回归覆盖发布 `opencode` 命令，而应用程序继续使用托管的 OpenCode 运行时合约，而不是新的容器特定引导路径。

### 从 Edge ACR 拉取

```bash
# 登录 Edge ACR
docker login hagicode.azurecr.io -u <username> -p <password>

# 拉取镜像
docker pull hagicode.azurecr.io/hagicode:1.2.3

# 运行容器
docker run -d -p 5000:5000 hagicode.azurecr.io/hagicode:1.2.3
```

### 使用 Claude Code 配置运行

```bash
docker run -d \
  -p 5000:5000 \
  -e ANTHROPIC_AUTH_TOKEN="sk-ant-..." \
  -v ~/claude-config:/claude-mount \
  hagicode.azurecr.io/hagicode:1.2.3
```

### 从 DockerHub 拉取

```bash
# 登录 DockerHub
docker login -u <username> -p <token>

# 拉取镜像
docker pull newbe36524/hagicode:1.2.3

# 运行容器
docker run -d -p 5000:5000 newbe36524/hagicode:1.2.3
```

### 从阿里云 ACR 拉取

```bash
# 登录阿里云 ACR
docker login registry.cn-hangzhou.aliyuncs.com -u <username> -p <password>

# 拉取镜像
docker pull registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode:1.2.3

# 运行容器
docker run -d -p 5000:5000 registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode:1.2.3
```

## GitHub Actions 工作流

### 自动触发

仓库包含自动触发的 GitHub Actions 工作流：

| 工作流 | 触发条件 | 描述 |
|----------|----------|-------------|
| `version-monitor.yml` | 推送到 main、定时（每 4 小时）| 监控 Azure Blob 中的新版本 |
| `github-release-workflow.yml` | 仓库调度（`version-monitor-release`）| 创建 GitHub releases |
| `docker-build.yml` | 标签推送（v*.*.*）、仓库调度（`version-monitor-docker`）| 构建并发布 Docker 镜像到 Azure ACR、阿里云 ACR 和 DockerHub |

### 发布结果追踪

`docker-build.yml` 工作流在成功向 Azure ACR 和阿里云 ACR 发布镜像后，自动生成发布结果 JSON 文件。此文件上传到 GitHub Release，包含：

- **Version**：发布的语义化版本
- **Published At**：发布时间的 ISO 8601 时间戳
- **Azure Registry**：Azure Container Registry URL
- **Azure Images**：已发布镜像 URL 数组（base、version、major/minor、major、latest 标签）
- **Aliyun Registry**：阿里云 Container Registry URL
- **Aliyun Images**：已发布到阿里云 ACR 的镜像 URL 数组
- **GitHub Run Info**：工作流运行 ID 和 URL，可追溯

发布结果文件名为 `publish-results-{version}.json`，可在 Release 的下载区域找到。

### 其他 Registry 集成

构建系统支持在成功构建并推送到 Azure ACR 后，将镜像推送到多个 registry。工作流自动推送到：

1. **阿里云 ACR**：使用 `docker buildx imagetools create` 将镜像从 Azure ACR 推送到阿里云 ACR
2. **DockerHub**：使用基于用户名的路径格式将镜像从 Azure ACR 推送到 DockerHub

#### 阿里云 ACR 推送流程

1. 使用 GitHub Secrets 中的凭据**登录阿里云 ACR**
2. 使用 `docker buildx imagetools create`**重新标记并推送**镜像从阿里云 ACR 到 Azure ACR
3. 为两个 registry **生成版本标签**（major.minor、major）
4. **处理预发布版本** - 为预发布版本（rc、beta、alpha、preview、dev）跳过 `latest` 标签

#### DockerHub 推送流程

1. 使用 GitHub Secrets 中的凭据**登录 DockerHub**
2. 使用 `docker buildx imagetools create`**重新标记并推送**镜像从 Azure ACR 到 DockerHub
3. 为两个 registry **生成版本标签**（major.minor、major）
4. **处理预发布版本** - 为预发布版本跳过 `latest` 标签

#### 镜像标签

对于**稳定发布**（例如 `1.2.3`）：
- `1.2.3` - 完整版本
- `1.2` - 主版本.次版本
- `1` - 主版本
- `latest` - 最新稳定版

对于**预发布版本**（例如 `1.2.3-rc.1`）：
- `1.2.3-rc.1` - 完整版本
- `1.2` - 主版本.次版本
- `1` - 主版本
- （无 `latest` 标签）

对于启用 Copilot 的容器，使用与现有发布相同的镜像发布流程。

#### 要求

**对于阿里云 ACR**，配置以下 GitHub Secrets：
- `ALIYUN_ACR_USERNAME` - 阿里云 ACR 用户名
- `ALIYUN_ACR_PASSWORD` - 阿里云 ACR 密码

**对于 DockerHub**，配置以下 GitHub Secrets：
- `DOCKERHUB_USERNAME` - DockerHub 用户名
- `DOCKERHUB_TOKEN` - DockerHub 访问令牌（在 https://hub.docker.com/settings/security 创建）

#### 发布结果 JSON Schema

```json
{
  "version": "1.2.3",
  "publishedAt": "2024-01-01T00:00:00Z",
  "github": {
    "runId": "1234567890",
    "runUrl": "https://github.com/.../actions/runs/...",
    "repository": "owner/repo"
  },
  "azure": {
    "registry": "hagicode.azurecr.io",
    "images": [
      {
        "name": "hagicode",
        "tag": "base",
        "fullUrl": "hagicode.azurecr.io/hagicode:base"
      },
      {
        "name": "hagicode",
        "tag": "1.2.3",
        "fullUrl": "hagicode.azurecr.io/hagicode:1.2.3"
      },
      ...
    ]
  },
  ...
}
```

### 仓库调度事件

工作流可以通过 repository_dispatch 事件编程触发：

| 事件类型 | 触发者 | 所需 Payload |
|------------|---------------|------------------|
| `version-monitor-release` | 版本监控器 | `{"version": "1.2.3"}` |
| `version-monitor-docker` | 版本监控器 | `{"version": "1.2.3"}` |

通过 GitHub CLI 触发示例：
```bash
# 触发 GitHub Release
gh api --method POST repos/{owner}/{repo}/dispatches \
  -F event_type='version-monitor-release' \
  -F client_payload='{"version": "1.2.3"}'

# 触发 Docker Build
gh api --method POST repos/{owner}/{repo}/dispatches \
  -F event_type='version-monitor-docker' \
  -F client_payload='{"version": "1.2.3"}'
```

### 手动工作流触发

您可以从 GitHub Actions UI 手动触发工作流：

#### Docker Build

1. 转到 **Actions** 选项卡
2. 选择 **Docker Multi-Arch Build and Push to Edge ACR**
3. 点击 **Run workflow**
4. 配置：
   - **Version**：`1.2.3`
   - **Platform**：`all`、`linux-amd64` 或 `linux-arm64`
   - **Dry Run**：启用以测试而不发布

#### 阿里云镜像同步

1. 转到 **Actions** 选项卡
2. 选择 **Azure to Aliyun Image Sync**
3. 点击 **Run workflow**
4. 配置：
   - **VERSION**：要同步的可选版本（例如 `v1.2.3` 或 `1.2.3`）
   - 留空以自动同步最新发布

## 配置

### 所需环境变量

详细配置请参阅 [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md)。

#### 最小所需变量

```bash
export AZURE_BLOB_SAS_URL="https://hagicode.blob.core.windows.net/packages?sp=..."
export GITHUB_TOKEN="ghp_xxxxxxxxxxxxxxxxxxxx"
export AZURE_ACR_USERNAME="hagicode"
export AZURE_ACR_PASSWORD="password"
export AZURE_ACR_REGISTRY="hagicode.azurecr.io"
```

#### 可选：DockerHub 配置

要将镜像推送到 DockerHub，请配置：

```bash
export DOCKERHUB_USERNAME="your-dockerhub-username"
export DOCKERHUB_TOKEN="your-dockerhub-access-token"
```

**注意**：DockerHub 访问令牌可在 https://hub.docker.com/settings/security 创建

#### 可选：阿里云 ACR 配置

要将镜像推送到阿里云 ACR，请配置：

```bash
export ALIYUN_ACR_USERNAME="your-aliyun-username"
export ALIYUN_ACR_PASSWORD="your-aliyun-password"
export ALIYUN_ACR_REGISTRY="registry.cn-hangzhou.aliyuncs.com"
export ALIYUN_ACR_NAMESPACE="hagicode"
```

### Nuke 参数

直接向 Nuke 传递参数：

```bash
./build.sh DockerRelease \
  --ReleaseVersion "1.2.3" \
  --DockerPlatform "all" \
  --DockerImageName "hagicode/hagicode"
```

## AI 代理集成

### 支持的代理

Docker 镜像包含预装的 AI 代理：

- **Claude Code CLI**：版本 2.1.71
- **OpenSpec CLI**：版本 1.2.0
- **UIPro CLI**：版本 2.2.3
- **Codex CLI**：版本 0.112.0
- **Copilot CLI**：版本 1.0.2

详细文档请参阅 [AGENTS.md](AGENTS.md)。

### Claude Code 配置

可以使用环境变量配置 Claude Code：

```bash
docker run -e ANTHROPIC_AUTH_TOKEN="sk-ant-..." hagicode.azurecr.io/hagicode:1.2.3
```

或者挂载主机配置：

```bash
docker run -v ~/claude-config:/claude-mount hagicode.azurecr.io/hagicode:1.2.3
```

### Codex 全局设置配置

Codex 连接通过容器环境变量配置。此更改不需要额外的应用端 API 密钥或 UI 配置。

```bash
docker run -d \
  -p 5000:5000 \
  -e CODEX_BASE_URL="https://api.openai.com/v1" \
  -e CODEX_API_KEY="sk-..." \
  hagicode.azurecr.io/hagicode:1.2.3
```

也支持兼容别名：

- `OPENAI_BASE_URL`（base URL 的回退别名）
- `OPENAI_API_KEY`（API 密钥的回退别名）

优先级：

- Base URL：`CODEX_BASE_URL` > `OPENAI_BASE_URL`
- API 密钥：`CODEX_API_KEY` > `OPENAI_API_KEY`

### Copilot CLI Docker 运行时兼容性

Copilot 运行时变量独立处理，不用于覆盖 Codex/OpenAI 运行时变量。

```bash
docker run -d \
  -p 5000:5000 \
  -e COPILOT_BASE_URL="https://api.githubcopilot.com" \
  -e COPILOT_API_KEY="ghp_..." \
  hagicode.azurecr.io/hagicode:1.2.3-copilot
```

Copilot 运行时变量：

- `COPILOT_BASE_URL`
- `COPILOT_API_KEY`

这些值与 Codex 优先级链隔离。

### CLI 版本覆盖（可选）

镜像包含固定的默认 CLI 版本。您可以通过入口点管理的环境变量在容器启动时覆盖特定 CLI 版本：

- `CLAUDE_CODE_CLI_VERSION`
- `OPENSPEC_CLI_VERSION`
- `UIPRO_CLI_VERSION`
- `CODEX_CLI_VERSION`
- `COPILOT_CLI_VERSION`

示例（仅覆盖 Codex 和 Copilot）：

```bash
docker run -d \
  -p 5000:5000 \
  -e CODEX_CLI_VERSION="0.112.0" \
  -e COPILOT_CLI_VERSION="1.0.2" \
  hagicode.azurecr.io/hagicode:1.2.3
```

如果未提供覆盖环境变量，则使用固定的镜像版本。

## Docker 构建基础设施

### Dockerfiles

| 文件 | 用途 | 平台 |
|------|-----------|----------|
| `docker_deployment/Dockerfile.template` | 统一 multi-stage 构建模板 | multi-arch |
| `docker_deployment/docker-entrypoint.sh` | 容器入口点 | all |
| `docker_deployment/.dockerignore` | 构建排除项 | all |

### 构建流程

1. **QEMU 设置**：安装 binfmt 用于跨架构构建
2. **Buildx Builder**：创建 Docker buildx builder 用于多架构
3. **统一镜像构建**：使用 multi-stage 构建统一 Docker 镜像（单个 Dockerfile 中的 base stage + final stage）
4. **Dockerfile 生成**：从模板生成 Dockerfile 并注入版本
5. **Azure ACR 推送**：将统一镜像带版本标签推送到 Azure ACR
6. **多架构验证**：验证镜像包含 amd64 和 arm64 两种架构
7. **其他 Registry 推送**：将镜像复制到阿里云 ACR 和 DockerHub 并验证

### 多 Registry 镜像发布

构建系统使用适配器模式支持将镜像推送到多个容器 registry：

| Registry | 路径格式 | 示例 |
|----------|-------------|---------|
| Azure ACR | `{registry}/{image}` | `hagicode.azurecr.io/hagicode` |
| 阿里云 ACR | `{registry}/{namespace}/{image}` | `registry.cn-hangzhou.aliyuncs.com/hagicode/hagicode` |
| DockerHub | `{username}/{image}` | `newbe36524/hagicode` |

Registry 推送独立配置 - 只有配置的 registry 才会被使用。

### 多架构验证

所有发布的 Docker 镜像都经过验证，确认包含 linux/amd64 和 linux/arm64 两种架构：

- **Azure ACR**：推送后，"Verify Images in Registry" 步骤检查两种架构的清单
- **阿里云 ACR**：同步后，"Verify Images in Aliyun ACR" 步骤验证所有推送的标签

如果缺少任一架构，工作流会失败并显示清晰的错误消息：
```
Error: Tag 1.2.3 does not contain amd64 architecture
```

这确保 AMD64 和 ARM64 平台上的用户都可以拉取兼容的镜像。

### 版本标签策略

镜像带有多个版本标签：

- **完整版本**：`1.2.3`（精确版本）
- **次版本**：`1.2`（主版本.次版本）
- **主版本**：`1`（主版本）
- **最新**：`latest`（始终指向最新）

版本 1.2.3 的标签示例：
```
hagicode.azurecr.io/hagicode:1.2.3
hagicode.azurecr.io/hagicode:1.2
hagicode.azurecr.io/hagicode:1
hagicode.azurecr.io/hagicode:latest
```

## 故障排除

### Docker 构建失败

**问题**：`docker: Error response from daemon: unknown`

**解决方案**：
1. 验证 Docker 正在运行：`docker ps`
2. 检查 Docker 版本：`docker --version`（需要 >= 20.10）
3. 启用 buildx：`docker buildx version`

### QEMU 设置失败

**问题**：`Failed to setup QEMU for cross-architecture builds`

**解决方案**：
```bash
# 手动安装 binfmt
docker run --privileged --rm tonistiigi/binfmt --install all
```

### Edge ACR 推送失败

**问题**：`unauthorized: authentication required`

**解决方案**：
1. 验证凭据正确
2. 手动登录：`docker login hagicode.azurecr.io`
3. 检查令牌是否过期

### 推送后镜像不可用

**问题**：推送后 registry 验证失败

**解决方案**：
1. 等待 registry 传播（可能需要 1-2 分钟）
2. 检查镜像是否存在：`docker manifest inspect hagicode.azurecr.io/hagicode:1.2.3`
3. 验证镜像摘要与推送输出一致

## 开发

### 构建系统（Nuke）

本仓库使用 [Nuke](https://nuke.build/) 进行构建自动化。

- **定义**：`nukeBuild/Build.cs`
- **目标**：跨多个文件分割（Targets.*.cs）
- **部分类**：状态和辅助函数在 `Build.Partial.cs` 中

### 添加新目标

要添加新的构建目标：

1. 创建 `nukeBuild/Build.Targets.YourTarget.cs`
2. 定义目标：
```csharp
Target YourTarget => _ => _
    .DependsOn(Download)
    .Executes(() => {
        // Your implementation
    });
```
3. 运行：`./build.sh YourTarget`

## 贡献

为本仓库贡献时：

1. 遵循现有代码风格和模式
2. 为新功能更新文档
3. 在 AMD64 和 ARM64 平台上测试
4. 确保记录环境变量
5. 更改 AI 代理版本时更新 AGENTS.md

## 文档

- [MIGRATION.md](MIGRATION.md) - 发布流程迁移指南
- [AGENTS.md](AGENTS.md) - AI 代理和集成
- [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md) - 完整环境变量参考

## 许可证

详见 [LICENSE](LICENSE) 文件。

## 支持

如有问题或疑问：

- 查看 [GitHub Issues](https://github.com/newbe36524/hagicode-release/issues)
- 查看 [AGENTS.md](AGENTS.md) 了解 AI 代理问题
- 查阅 [ENVIRONMENT_VARIABLES.md](ENVIRONMENT_VARIABLES.md) 获取配置帮助
