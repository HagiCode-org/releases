# 实施任务清单

## 任务进度

- [ ] **TASK-1**: 审查 `EffectiveGitHubToken` 的完整依赖链和现有使用模式
- [ ] **TASK-2**: 修改 `VersionMonitor` target 移除无效的 `.Requires()` 声明
- [ ] **TASK-3**: 本地验证构建成功
- [ ] **TASK-4**: 验证其他 target 不受影响

## 详细任务

### TASK-1: 审查 `EffectiveGitHubToken` 的完整依赖链和现有使用模式

**目标**: 确保 `EffectiveGitHubToken` 的所有基础依赖都正确配置了注入特性，并参考现有正确使用模式。

**步骤**:
1. 读取 `nukeBuild/Build.Partial.cs` 确认 `EffectiveGitHubToken` 的实现
2. 验证 `GitHubToken` 参数是否具有 `[Parameter]` 和 `[Secret]` 特性
3. 确认 `GitHubActions` 由 Nuke 自动注入（无需额外配置）
4. **重点**: 审查 `nukeBuild/Build.Targets.GitHub.cs` 中 `GitHubRelease` target 的正确实现模式：
   - 注意 `GitHubRelease` **不使用** `.Requires(() => EffectiveGitHubToken)`
   - 观察它在执行体内如何使用 `var token = EffectiveGitHubToken;` (第 26 行)
   - 观察如何通过环境变量传递: `["GH_TOKEN"] = token` (第 66 行)
5. 确认 `VersionMonitor` target 已经在执行体内正确使用 `EffectiveGitHubToken` 和 `GH_TOKEN` 环境变量

**验证标准**:
- `GitHubToken` 参数存在且标记正确
- `EffectiveGitHubToken` 计算逻辑清晰（优先 CI token）
- `GitHubRelease` target 作为正确参考模式已被理解

**预计耗时**: 短

---

### TASK-2: 修改 `VersionMonitor` target

**目标**: 移除导致构建失败的 `.Requires(() => EffectiveGitHubToken)` 声明。

**步骤**:
1. 打开 `nukeBuild/Build.Targets.VersionMonitor.cs`
2. 找到 `VersionMonitor` target 定义
3. 移除 `.Requires(() => EffectiveGitHubToken)` 行
4. 保持其他 `.Requires()` 声明不变

**代码变更**:
```diff
Target VersionMonitor => _ => _
    .Requires(() => AzureBlobSasUrl)
-   .Requires(() => EffectiveGitHubToken)
    .Requires(() => EffectiveGitHubRepository)
    .Executes(VersionMonitorExecute);
```

**验证标准**:
- 文件已修改，仅移除指定行
- 其他代码保持不变

**预计耗时**: 短

---

### TASK-3: 本地验证构建成功

**目标**: 确保 Nuke build 能够成功加载并识别 `VersionMonitor` target。

**步骤**:
1. 执行 `dotnet run --project nukeBuild` 查看所有可用 target
2. 确认 `VersionMonitor` 出现在 target 列表中
3. 尝试执行 `dotnet run --project nukeBuild -- VersionMonitor --help` 查看 target 参数

**注意**: 完整执行可能需要 `AzureBlobSasUrl` 等参数，但仅验证 target 可加载即可。

**验证标准**:
- Nuke 成功加载所有 target
- 不再出现 "Member 'EffectiveGitHubToken' is required..." 错误
- `VersionMonitor` target 可见且可查看帮助信息

**预计耗时**: 短

---

### TASK-4: 验证其他 target 不受影响

**目标**: 确保使用 `EffectiveGitHubToken` 的其他 target 继续正常工作。

**步骤**:
1. 使用 grep 搜索 `EffectiveGitHubToken` 的所有使用位置
2. 检查 `GitHubRelease` target (`Build.Targets.GitHub.cs`) 的实现作为参考
3. 确认 `VersionMonitor` target 在执行体内使用 `EffectiveGitHubToken` 的方式与 `GitHubRelease` 一致：
   - 在执行体内直接使用（不通过 `.Requires()`）
   - 通过 `GH_TOKEN` 环境变量传递给 GitHub CLI
4. 确认其他使用 `EffectiveGitHubToken` 的 target 不依赖 `.Requires(() => EffectiveGitHubToken)`

**验证标准**:
- 所有 `EffectiveGitHubToken` 的使用都在 target 执行体内部（非 `.Requires()`）
- GitHub Token 逻辑在所有 target 中保持一致：执行体内使用 + 环境变量传递

**预计耗时**: 短

## 完成标准

所有任务完成后，应满足：

1. ✅ `VersionMonitor` target 可被 Nuke 正确加载
2. ✅ 本地 `dotnet run --project nukeBuild` 不显示注入错误
3. ✅ GitHub Token 获取逻辑保持不变（CI token 优先，参数回退）
4. ✅ 其他 GitHub 相关 target 功能不受影响

## 后续步骤

任务完成后，`version-monitor.yml` 工作流应能正常运行。建议：
- 在 GitHub Actions 中测试手动触发工作流
- 监控定时执行（每 4 小时）是否正常工作
