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

## 本地容器构建与测试

仓库根目录现在额外提供了一套面向本地调试的 `docker compose` 工作流，不会绕开现有 Nuke build context 生成逻辑：

```bash
cp .env.local.example .env.local
cp .env.secrets.local.example .env.secrets.local
./scripts/docker-local-build.sh
./scripts/docker-local-up.sh
./scripts/docker-local-test.sh
./scripts/docker-local-logs.sh
./scripts/docker-local-down.sh
```

- `docker-compose.local.yml` 使用本地镜像标签 `HAGICODE_LOCAL_IMAGE`，默认把应用发布到 `127.0.0.1:5000`
- 本地持久化目录固定落在 `./.local/hagicode/data` 与 `./.local/hagicode/saves`
- 本地专用的明文凭据建议放在 `.env.secrets.local`；本地脚本会在 `.env.local` 之后加载它，`build.sh` / `build.ps1` 在非 GitHub Actions 环境下也会自动加载
- 如果设置了 `AZURE_BLOB_SAS_URL`，`scripts/docker-local-build.sh` 会先下载指定版本和平台的包；否则会复用 `output/download` 中已经存在的 zip 包
- 本地镜像构建仍然依赖 Docker Hub、`dot.net`、GitHub 与 npm 的出站访问，除非你的机器已经准备好了等价的镜像源或缓存
- `scripts/docker-local-test.sh` 会等待 HTTP 就绪，并额外检查 HagiScript 同步后的运行时基线：`hagiscript`、`claude`、`openspec`、`skills`、`opencode` 与 `codex` 是否都能在容器内执行

## 容器运行时契约

统一运行时镜像现在从纯净的 `debian:bookworm-slim` 基础镜像构建，不再继承官方 `node` 镜像的默认用户模型。Node.js 22 通过镜像自管的 NVM 布局安装到 `/usr/local/nvm`，而通过 npm 交付的内置 CLI 仍安装在 `/home/hagicode/.npm-global`。
镜像构建时，Node 引导层会先清理 `NPM_CONFIG_PREFIX` 再执行 `nvm install`；切换到 `hagicode` 用户后，再通过 `npm config set prefix '/home/hagicode/.npm-global'` 恢复 npm CLI 的运行时和全局安装约定。
随后镜像会先安装固定版本的 `@hagicode/hagiscript`，再运行 `hagiscript npm-sync --managed-runtime /home/hagicode/.hagiscript/node-runtime --manifest /app/bootstrap/hagiscript-sync-manifest.json` 同步其余内置依赖基线。发布仓库内的 manifest 选择 `claude-code`、`fission-openspec`、`opencode` 与 `codex` 这些 optional built-in agent CLI，而 `skills` 则保留在内置工具基线中。
HagiScript 托管运行时已经加入 `PATH`，因此同步后的命令不需要入口脚本在启动时重新安装。

容器中唯一受支持的非 root 运行用户是 `hagicode`。当提供 `PUID` 和 `PGID` 时，启动脚本只会重映射这一个用户，并修正 `/home/hagicode`、其 `.claude` 状态目录以及 `/app` 的所有权。

统一运行时镜像内置的主要 agent CLI 基线仅包含：

- `claude`
- `opencode`
- `codex`

`openspec` 仍作为镜像保留的工作流工具存在，`skills` 也作为镜像保留的技能管理 CLI 默认内置；二者都通过同一套 HagiScript catalog-backed 基线同步，并与主要 agent CLI 基线分开表述，避免再次把更多 provider CLI 误解为默认内置能力。

Docker 入口脚本现在只校验保留的 HagiScript 同步 CLI 基线、解析 HagiCode 应用入口、应用 Claude 运行时配置，然后直接启动应用。`omniroute` 与 `code-server` 不再视为 release 镜像的内置支持，运行时也不再依赖 `pm2` 去编排多进程启动链路。

像 `copilot`、`codebuddy`、`qodercli` 这样的 provider CLI 现在都走 HagiCode UI 管理的安装路径，不再作为容器默认内置能力。`uipro` 也不再随镜像发布，因为对应能力已经由内置的 `skills` 命令接管。

## 运行时启动与持久化

从 release 镜像视角看，容器启动现在是单进程模型：入口脚本准备运行时前置条件后，会直接用 `dotnet` 启动识别到的 HagiCode 应用程序集。

- 生产部署必须同时持久化这两个根目录：`hagicode_data:/app/data` 负责保持 system-scoped 资源可写，`hagicode_saves:/app/saves` 负责保持 save-scoped 运行时状态可写
- save-scoped 的 HagiCode 运行时状态通过 `hagicode_saves:/app/saves` 持久化，活动存档根目录位于 `/app/saves/save0/...`
- 镜像与入口脚本只会准备 `/app/data` 和 `/app/saves`；`/app/saves/save0/config` 与 `/app/saves/save0/data` 仍由应用在初始化活动存档时按需创建
- 如果你是从旧的单卷部署升级，请在替换容器前先补充 `/app/saves` 的 named volume 或 bind mount

最小挂载布局：

```yaml
volumes:
  - hagicode_data:/app/data
  - hagicode_saves:/app/saves
```

## 启动阶段 SSH 引导

发布镜像现在安装了 `openssh-client`，并且可以在明确需要 SSH 访问时于启动阶段导入挂载的私钥。

- 设置 `SSH_PRIVATE_KEY_PATH` 指向挂载的私钥文件即可启用引导
- 可选设置 `SSH_KNOWN_HOSTS_PATH` 导入挂载的 `known_hosts`
- 可选设置 `SSH_STRICT_HOST_KEY_CHECKING` 覆盖默认的 `accept-new`
- 若不设置 `SSH_PRIVATE_KEY_PATH`，容器会跳过 SSH 引导并继续原有启动流程

启动时，入口脚本会把私钥复制到 `/home/hagicode/.ssh/imported_key`，在 `/home/hagicode/.ssh/config` 写入确定性的 SSH 配置，修正 `hagicode` 运行用户所需的所有权与权限，并导出 `GIT_SSH_COMMAND`，让后续 `git` 与 `ssh` 调用默认使用导入的身份文件。

如果显式设置了 `SSH_PRIVATE_KEY_PATH`，但对应文件不存在、不可读或不是常规文件，容器会在应用启动前快速失败，并输出路径级诊断信息，但不会打印任何私钥内容。

## 在生态中的角色

HagiCode Release 接收 `repos/hagicode-core`、`repos/hagicode-desktop` 等仓库生成的构建产物，并把它们发布到 GitHub Releases、阿里云 ACR、DockerHub 等交付渠道。
