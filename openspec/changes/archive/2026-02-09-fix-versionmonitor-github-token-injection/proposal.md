# 修复 VersionMonitor 的 GitHub Token 注入问题

**提案 ID**: `fix-versionmonitor-github-token-injection`
**状态**: ExecutionCompleted
**创建日期**: 2026-02-08
**优先级**: High

## 概述

修复 `VersionMonitor` Nuke target 中的依赖注入问题。该 target 在执行时因 `EffectiveGitHubToken` 成员未使用适当的 Nuke 注入特性标记而导致构建失败。

## 问题陈述

### 错误信息

```
Member 'EffectiveGitHubToken' is required from target 'VersionMonitor' but not marked with an injection attribute.
Build failed on 02/08/2026 15:42:53. (╯°□°）╯︵ ┻━┻
Error: Process completed with exit code 255.
```

### 根本原因

在 `nukeBuild/Build.Targets.VersionMonitor.cs` 中，`VersionMonitor` target 使用 `.Requires(() => EffectiveGitHubToken)` 声明了对 `EffectiveGitHubToken` 的依赖。

然而，`EffectiveGitHubToken` 在 `Build.Partial.cs` 中被定义为计算属性（使用表达式主体语法）：

```csharp
/// <summary>
/// Gets the GitHub token from CI or parameter
/// </summary>
string EffectiveGitHubToken => GitHubActions?.Token ?? GitHubToken;
```

Nuke 的依赖注入系统无法自动识别这种计算属性的依赖关系，因为它没有使用标准的注入特性（如 `[Parameter]`、`[Secret]` 等）进行标记。

### 影响范围

- **受影响组件**: `VersionMonitor` target
- **受影响工作流**: `.github/workflows/version-monitor.yml`
- **功能影响**: 版本监控功能完全不可用，无法自动检测新版本并触发发布流程

## 提议的解决方案

### 方案选择

根据 Nuke 的依赖注入模式和现有代码结构，最简单直接的解决方案是：

**移除 `VersionMonitor` target 中对 `EffectiveGitHubToken` 的显式 `.Requires()` 声明**。

### 技术理由

1. **现有正确模式参考**: `GitHubRelease` target (`Build.Targets.GitHub.cs`) 已经成功使用了 `EffectiveGitHubToken`，其模式是：
   - **不使用** `.Requires(() => EffectiveGitHubToken)`
   - 在执行体内直接使用 `var token = EffectiveGitHubToken;`
   - 通过环境变量传递给 GitHub CLI: `["GH_TOKEN"] = token`

2. `EffectiveGitHubToken` 是一个计算属性，其值来源于：
   - `GitHubActions?.Token` - 由 Nuke 在 GitHub Actions 环境中自动提供
   - `GitHubToken` - 已正确标记为 `[Parameter]` 和 `[Secret]`

3. 当 target 执行时，这些基础依赖已经被 Nuke 的注入系统正确解析，因此 `EffectiveGitHubToken` 可以正常计算。

4. `.Requires()` 主要用于确保 Nuke 在执行 target 前验证参数可用性，但对于计算属性来说，这种验证是多余的，因为其依赖已被注入系统处理。

### 实现变更

修改 `nukeBuild/Build.Targets.VersionMonitor.cs`:

```diff
Target VersionMonitor => _ => _
    .Requires(() => AzureBlobSasUrl)
-   .Requires(() => EffectiveGitHubToken)
    .Requires(() => EffectiveGitHubRepository)
    .Executes(VersionMonitorExecute);
```

**注意**: `VersionMonitor` target 已经在执行体内正确使用 `EffectiveGitHubToken` 并通过环境变量 `GH_TOKEN` 传递给 GitHub CLI（第 84 行和第 172 行），这与 `GitHubRelease` target 的模式一致。

## 实施计划

详见 [`tasks.md`](./tasks.md)。

## 验证标准

### 功能验证

1. 本地执行 `dotnet run --project nukeBuild -- VersionMonitor` 应能成功启动（假设提供必要参数）
2. 在 GitHub Actions 环境中，`version-monitor.yml` 工作流应能正常运行

### 回归测试

1. 其他使用 `EffectiveGitHubToken` 的 target（如 `CreateGitHubRelease`）不应受影响
2. GitHub Token 的获取逻辑（优先 CI token，回退到参数）应保持不变

## 风险评估

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|----------|
| 移除 `.Requires()` 后运行时缺少 token | 低 | 高 | 验证 `GitHubToken` 参数已正确标记为 `[Secret]` |
| 其他 target 依赖此模式 | 低 | 中 | 审查其他 target 的实现 |

## 替代方案（不采用）

### 方案 A: 将 `EffectiveGitHubToken` 改为属性

使用 `[MemberConfiguration]` 特性将计算属性暴露给 Nuke 注入系统。**缺点**: 增加复杂性，不符合 Nuke 标准模式。

### 方案 B: 直接使用 `GitHubToken` 参数

在 `VersionMonitor` 中直接使用 `GitHubToken` 而非 `EffectiveGitHubToken`。**缺点**: 失去 CI token 优先的灵活性。

## 参考资料

- [Nuke Injection Attributes Documentation](https://nuke.build/docs/injection/)
- [Build.Targets.VersionMonitor.cs](../../nukeBuild/Build.Targets.VersionMonitor.cs) - 需要修复的文件
- [Build.Targets.GitHub.cs](../../nukeBuild/Build.Targets.GitHub.cs) - 正确使用 `EffectiveGitHubToken` 的参考实现
- [Build.Partial.cs](../../nukeBuild/Build.Partial.cs) - `EffectiveGitHubToken` 定义
- [version-monitor.yml](../../.github/workflows/version-monitor.yml) - 受影响的工作流
