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
- `scripts/docker-local-test.sh` 会等待 HTTP 就绪，并额外检查 `claude`、`openspec`、`skills`、`opencode`、`codex` 与 `code-server` 是否都能在容器内执行

## 容器运行时契约

统一运行时镜像现在从纯净的 `debian:bookworm-slim` 基础镜像构建，不再继承官方 `node` 镜像的默认用户模型。Node.js 22 通过镜像自管的 NVM 布局安装到 `/usr/local/nvm`，而通过 npm 交付的内置 CLI 仍安装在 `/home/hagicode/.npm-global`。
镜像构建时，Node 引导层会先清理 `NPM_CONFIG_PREFIX` 再执行 `nvm install`；切换到 `hagicode` 用户后，再通过 `npm config set prefix '/home/hagicode/.npm-global'` 恢复 npm CLI 的运行时和全局安装约定。
`code-server` 会通过固定版本的 standalone 发布包安装并挂到 `PATH`，因此不依赖 npm 全局 prefix。

容器中唯一受支持的非 root 运行用户是 `hagicode`。当提供 `PUID` 和 `PGID` 时，启动脚本只会重映射这一个用户，并修正 `/home/hagicode`、其 `.claude` 状态目录以及 `/app` 的所有权。

统一运行时镜像内置的主要 agent CLI 基线仅包含：

- `claude`
- `opencode`
- `codex`

`openspec` 仍作为镜像保留的工作流工具存在，`skills` 也作为镜像保留的技能管理 CLI 默认内置；它们都会与主要 agent CLI 基线分开表述，避免再次把更多 provider CLI 误解为默认内置能力。

像 `copilot`、`codebuddy`、`qodercli` 这样的 provider CLI 现在都走 HagiCode UI 管理的安装路径，不再作为容器默认内置能力。`uipro` 也不再随镜像发布，因为对应能力已经由内置的 `skills` 命令接管。

## Omniroute 统一 provider 引导

发布镜像现在把 Omniroute 作为容器内部 Claude、Codex/OpenAI 与 OpenCode 流量的统一本地 provider proxy。

- 默认本地监听：`127.0.0.1:4060`
- 本地管理与运行时状态目录：`/app/data/omniroute`
- 共享进程托管层：`pm2-runtime`
- 应用就绪门禁文件：`/app/data/omniroute/runtime/hagicode.ready`

启动顺序固定为 Omniroute 优先：

1. 入口脚本先解析 HagiCode 应用启动命令，并在任何本地回环改写发生前捕获上游 provider 凭据。
2. 入口脚本规范化 Omniroute 运行路径，并把持久化密钥写入 `/app/data/omniroute`。
3. 入口脚本把本地 Omniroute 端点重新导出到 `claude`、`codex`、`opencode` 与 HagiCode 使用的运行时环境变量中。
4. `pm2-runtime` 统一启动两个托管进程：`omniroute` 与 `hagicode-app`。
5. `hagicode-app` 会通过 `wait-for-ready.sh` 启动，在 Omniroute bootstrap 成功并写入 ready file 前一直阻塞。
6. bootstrap helper 会登录本地 Omniroute，通过 HTTP API 幂等 upsert provider node 与 connection，写入 bootstrap state，然后释放 ready file。

整个 bootstrap 过程是 API-first 且幂等的。容器不会直接修改 Omniroute 的 SQLite 文件，而是复用持久化状态，只对已经具备最小上游凭据的 provider 执行 upsert。

bootstrap 完成后，容器会把主要 provider 端点统一回环到本地 Omniroute：

- `ANTHROPIC_URL` 会改写为本地 Omniroute API 基址，`ANTHROPIC_AUTH_TOKEN` 会替换为本地共享密钥，供 Claude CLI 走统一代理。
- `CODEX_BASE_URL` / `OPENAI_BASE_URL` 会改写为本地 Omniroute API 基址，`CODEX_API_KEY` / `OPENAI_API_KEY` 会替换为本地共享密钥。
- `OPENCODE_BASE_URL` / `OPENCODE_API_BASE_URL` 会改写为本地 Omniroute API 基址，`OPENCODE_API_KEY` 会替换为本地共享密钥。
- HagiCode 会收到 `HAGICODE_OMNIROUTE_ENABLED=true`、`HAGICODE_OMNIROUTE_BASE_URL`、`HAGICODE_OMNIROUTE_API_BASE_URL`、`OmniRoute__Enabled`、`OmniRoute__BaseUrl` 与 `OmniRoute__ApiBaseUrl`。

## 内置 Code Server 运行时

统一镜像现在会把固定版本的 `code-server` 一并内置到运行时基线里，因此 Builder 可以直接导出浏览器 IDE 默认值，而不需要部署者在容器启动后再手工安装额外软件。

- 当 Builder 处于 `full-custom` 模式并保持启用 code-server 时，会直接导出 `VsCodeServer__*` 默认值
- Builder 现在额外提供一个共享的 EULA 开关；只有显式勾选后才会导出 `ACCEPT_EULA=Y`，否则入口脚本会拒绝继续启动
- 专用宿主机端口发布仍然是显式开启能力，生成的首层映射默认固定绑定到 `127.0.0.1`
- 如果使用密码认证，则必须提供 `CODE_SERVER_PASSWORD` 或 `CODE_SERVER_HASHED_PASSWORD`；入口脚本会在应用启动前桥接到标准的 `PASSWORD` / `HASHED_PASSWORD`
- 生产部署必须同时持久化这两个根目录：`hagicode_data:/app/data` 负责保持 system-scoped 资源可写，`hagicode_saves:/app/saves` 负责保持 save-scoped 运行时状态可写
- 系统级资源仍然通过 `hagicode_data:/app/data` 持久化，托管的 Code Server 运行时数据继续保留在 `/app/data/code-server`
- save-scoped 的 HagiCode 运行时状态现在通过 `hagicode_saves:/app/saves` 持久化，活动存档根目录位于 `/app/saves/save0/...`
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

HagiCode Release 接收 `repos/hagicode-core`、`repos/hagicode-desktop` 等仓库生成的构建产物，并把它们发布到 GitHub Releases、Azure ACR、阿里云 ACR、DockerHub 等交付渠道。
