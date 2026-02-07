# 实施任务清单

修复 Docker 构建失败问题 - lib/ 目录复制错误

---

## 任务 1：验证当前问题

**目标**：通过分析 `output/` 目录确认 lib/ 目录在 Docker 构建上下文中的结构问题

**问题诊断方法**：
本提案通过分析项目的输出目录结构来诊断问题：
- `output/extracted/` - 从 Azure Blob Storage 下载并解压的原始包内容
- `output/docker-context/` - 准备给 Docker 构建使用的上下文目录

通过对比这两个目录的结构，可以定位 `lib/` 目录复制的问题。

**步骤**：
1. 检查 `output/extracted/lib/` 目录是否存在（源目录）
2. 检查 `output/docker-context/` 目录内容（目标目录）
3. 确认 lib/ 内容被直接复制到根目录而非 lib/ 子目录
4. 验证 Dockerfile 中 `COPY lib/ /app/` 命令期望的目录结构

**验证命令**：
```bash
# 检查源目录（应该包含 lib/）
ls -la output/extracted/
ls -la output/extracted/lib/

# 检查目标上下文目录（lib/ 内容被平铺到根目录）
ls -la output/docker-context/

# 验证 lib/ 子目录不存在
test -d output/docker-context/lib && echo "lib/ exists" || echo "lib/ missing"
```

**当前状态（已验证）**：
- `output/extracted/lib/` ✓ 存在，包含所有 DLL 文件
- `output/docker-context/lib/` ✗ 不存在
- `output/docker-context/` 包含 lib/ 的内容（DLL 文件平铺在根目录）

**预期结果**：确认问题存在 - lib/ 内容在 docker-context 根目录而非 lib/ 子目录

---

## 任务 2：修改 PrepareDockerBuildContext 方法

**目标**：修复 lib/ 目录复制逻辑

**文件**：`nukeBuild/Build.Targets.Docker.cs`

**修改位置**：第 92-98 行

**修改前**：
```csharp
// Copy lib/ directory (framework-dependent assemblies)
var libDir = ExtractedDirectory / "lib";
if (!System.IO.Directory.Exists(libDir))
{
    throw new Exception($"Required lib/ directory not found in extracted package at {libDir}");
}
CopyDirectoryRecursive(libDir, DockerBuildContext);
```

**修改后**：
```csharp
// Copy lib/ directory (framework-dependent assemblies)
var libDir = ExtractedDirectory / "lib";
if (!System.IO.Directory.Exists(libDir))
{
    throw new Exception($"Required lib/ directory not found in extracted package at {libDir}");
}

// Create lib/ subdirectory in build context
var targetLibDir = DockerBuildContext / "lib";
targetLibDir.CreateDirectory();
CopyDirectoryRecursive(libDir, targetLibDir);
```

---

## 任务 3：验证修复

**目标**：确认 Docker 构建上下文结构正确

**步骤**：
1. 运行 `nuke Clean Extract DockerBuild`
2. 检查 `output/docker-context/lib/` 目录是否存在
3. 确认 lib/ 内容在 lib/ 子目录中

**验证命令**：
```bash
ls -la output/docker-context/
ls -la output/docker-context/lib/
```

**预期结果**：
```
output/docker-context/
├── Dockerfile
├── docker-entrypoint.sh
└── lib/
    ├── PCode.Web.dll
    └── [其他文件...]
```

---

## 任务 4：执行完整构建测试

**目标**：验证 Docker 镜像成功构建

**步骤**：
1. 运行 `nuke DockerBuild`
2. 确认无错误发生
3. 验证镜像已创建

**验证命令**：
```bash
docker images | grep hagicode
```

**成功标准**：
- DockerBuild 目标完成无错误
- 镜像列表中出现 hagicode 镜像
- 镜像标签正确

---

## 任务 5：清理和验证

**目标**：确保构建系统状态一致

**步骤**：
1. 清理输出目录：`nuke Clean`
2. 执行完整发布流程测试（可选）
3. 提交修复

**最终验证**：
```bash
nuke Clean
nuke Download
nuke Extract
nuke DockerBuild
```

---

## 任务摘要

| 任务 | 状态 | 优先级 | 备注 |
|------|------|--------|------|
| 1. 验证当前问题 | ✅ 已完成 | 高 | 通过代码分析确认问题根因 |
| 2. 修改 PrepareDockerBuildContext | ✅ 已完成 | 高 | 已创建 lib/ 子目录并修复复制逻辑 |
| 3. 验证修复 | ⏸️ 待验证 | 高 | 需要 Azure 包下载后运行 nuke Clean Extract DockerBuild |
| 4. 执行完整构建测试 | ⏸️ 待验证 | 高 | 需要 Azure 包和 Docker 环境 |
| 5. 清理和验证 | ⏸️ 待验证 | 中 | 完整构建后执行 |

---

## 实施完成摘要

### 已完成的代码修改

**文件**: `nukeBuild/Build.Targets.Docker.cs:92-102`

**修改内容**:
- 修改前: `CopyDirectoryRecursive(libDir, DockerBuildContext);` 将 lib/ 内容平铺到 docker-context 根目录
- 修改后:
  ```csharp
  // Create lib/ subdirectory in build context
  var targetLibDir = DockerBuildContext / "lib";
  targetLibDir.CreateDirectory();
  CopyDirectoryRecursive(libDir, targetLibDir);
  ```

### 待执行验证步骤（需要实际构建环境）

```bash
# 1. 清理并准备构建环境
nuke Clean

# 2. 下载并解压包
nuke Download
nuke Extract

# 3. 验证 docker-context/lib/ 目录结构
ls -la output/docker-context/
ls -la output/docker-context/lib/

# 4. 执行 Docker 构建
nuke DockerBuild

# 5. 验证镜像创建成功
docker images | grep hagicode
```
