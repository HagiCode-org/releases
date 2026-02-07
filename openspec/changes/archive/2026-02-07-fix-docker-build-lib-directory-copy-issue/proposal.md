# 修复Docker构建lib目录复制问题

## 概述

修复 `DockerBuild` 目标失败的问题。错误为 `failed to calculate checksum of ref: "/lib": not found`，原因是 Docker 构建上下文中 `lib/` 目录结构不正确。

## 背景

Hagicode Release 是一个仅发布的仓库，使用 Nuke 构建系统自动化发布流程：

```
Azure Blob Storage → 下载包 → 解压 → 准备 Docker 构建上下文 → 构建 Docker 镜像 → 推送到 Docker Hub
```

### 构建流程

1. `Download` 目标从 Azure Blob Storage 下载预构建包
2. `Extract` 目标解压包到 `output/extracted/`（包含 `lib/` 目录）
3. `PrepareDockerBuildContext` 方法准备 Docker 构建上下文
4. `DockerBuild` 目标构建 Docker 镜像

## 问题分析

### 当前行为

`PrepareDockerBuildContext` 方法（`nukeBuild/Build.Targets.Docker.cs:92-98`）执行：

```csharp
var libDir = ExtractedDirectory / "lib";
CopyDirectoryRecursive(libDir, DockerBuildContext);
```

`CopyDirectoryRecursive` 方法将 `lib/` 目录的**内容**直接复制到 `DockerBuildContext` 根目录，而不是保留 `lib/` 子目录结构。

### 实际结果

```
output/docker-context/
├── Dockerfile
├── docker-entrypoint.sh
├── PCode.Web.dll
├── appsettings.yml
└── [其他 .dll 文件...]  ← lib/ 内容直接在根目录
```

### Dockerfile 期望

```dockerfile
COPY --chown=hagicode:hagicode lib/ /app/
```

Dockerfile 期望 `lib/` 目录存在于构建上下文中，但实际不存在，导致构建失败。

### 错误信息

```
ERROR: failed to calculate checksum of ref: "/lib": not found
```

## 解决方案

### 方案选择：保留 lib/ 目录结构

修改 `PrepareDockerBuildContext` 方法，在构建上下文中创建 `lib/` 子目录：

```csharp
// 创建 lib/ 子目录
var targetLibDir = DockerBuildContext / "lib";
targetLibDir.CreateDirectory();

// 将 lib/ 内容复制到 lib/ 子目录
CopyDirectoryRecursive(libDir, targetLibDir);
```

### 为什么选择此方案

1. **语义清晰**：`lib/` 是一个逻辑单元，应作为独立目录存在
2. **与 Dockerfile 匹配**：无需修改 Dockerfile 模板
3. **可维护性**：保持构建脚本与 Dockerfile 的一致性
4. **最小变更**：只修改一处代码，风险最小

## 影响范围

### 修改文件

- `nukeBuild/Build.Targets.Docker.cs` - 修改 `PrepareDockerBuildContext` 方法

### 不受影响

- Docker 模板文件
- 基础镜像构建
- 其他构建目标

## 预期结果

修复后，`DockerBuild` 目标应成功完成，Docker 构建上下文结构为：

```
output/docker-context/
├── Dockerfile
├── docker-entrypoint.sh
└── lib/
    ├── PCode.Web.dll
    ├── appsettings.yml
    └── [其他 .dll 文件...]
```

## 风险评估

- **风险级别**：低
- **影响范围**：仅限 Docker 应用程序镜像构建
- **回滚难度**：简单（单行代码变更）

## 相关链接

- 错误堆栈：参见问题描述
- 相关代码：`nukeBuild/Build.Targets.Docker.cs:78-107`
