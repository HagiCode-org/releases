# Change: 修复 VersionMonitor jq 表达式错误

## Status: ExecutionCompleted

## Why

VersionMonitor 目标在执行时失败，无法完成 GitHub releases 的获取操作。错误信息显示 `gh release list` 命令中的 jq 表达式格式错误，导致版本监控工作流完全无法运行。

**根本原因**：
在 `GetGitHubReleases` 方法中传递给 GitHub CLI 的 jq 表达式存在格式错误。当前代码将整个 jq 表达式作为单个字符串传递给 `Arguments` 属性，导致命令行解析错误。

## What Changes

- 修复 `Build.Targets.VersionMonitor.cs` 中 `GetGitHubReleases` 方法的命令参数格式
- 将 jq 表达式正确传递给 `gh release list` 命令
- 添加调试日志以验证命令执行

**技术细节**：
- 当前问题：`Arguments = "release list --json tagName --jq '.[].tagName'"`
- 修复后：使用 `ArgumentList` 正确传递参数数组

## Impact

- Affected specs: `version-monitor`
- Affected code: `nukeBuild/Build.Targets.VersionMonitor.cs:68-113` (`GetGitHubReleases` 方法)

**修复后的预期效果**：

1. **VersionMonitor 目标成功执行**：能够正确获取 GitHub releases 列表
2. **自动版本监控恢复**：定期工作流可以检测 Azure Blob Storage 中的新版本
3. **手动触发可用**：用户可以手动执行 VersionMonitor 进行版本检查
4. **完整的发布流程**：修复后将实现完整的"监控→检测→触发"自动化链路
