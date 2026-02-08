# Change: 使用 Nuke 重构版本监控功能

## Why

当前版本监控功能 (Version Monitor) 使用独立的 GitHub Actions 工作流实现，与项目现有的 Nuke 构建系统分离，导致以下问题：

1. **一致性问题**: 自定义脚本与 Nuke 构建系统分离，缺乏统一的构建目标和依赖管理
2. **可维护性问题**: 不遵循项目的模块化架构模式，难以与其他构建目标集成
3. **代码重复**: Azure Blob Storage 操作逻辑在工作流中重复实现，而非复用现有的 `AzureBlobAdapter.cs`

## What Changes

- **创建 `VersionMonitor` Nuke 目标**: 新增 `Build.Targets.VersionMonitor.cs` 文件，实现版本监控功能
- **复用现有适配器**: 利用 `AzureBlobAdapter.cs` 进行 Azure Blob Storage 操作
- **新增辅助方法**: 在 `AzureBlobAdapter.cs` 中添加获取所有版本列表的方法
- **更新 GitHub Actions 工作流**: 修改 `.github/workflows/version-monitor.yml` 以调用 Nuke 目标
- **添加必要的环境变量**: 在 `.env.example` 中添加 GitHub Token 相关配置
- **支持 Dry Run 模式**: 添加 `--DryRun` 参数用于测试

### 技术实现

1. **扩展 `AzureBlobAdapter.cs`**
   - 添加 `GetAllVersions(PackageIndex index)` 方法：从 index.json 中提取所有版本列表

2. **创建 `Build.Targets.VersionMonitor.cs`**
   - 定义 `VersionMonitor` 构建目标
   - 实现版本比较逻辑（Azure 版本 vs GitHub Releases）
   - 支持触发发布工作流（通过 `repository_dispatch` 事件）
   - 输出监控结果摘要

3. **更新 `version-monitor.yml` 工作流**
   - 使用 `nuke VersionMonitor` 替代现有的 bash 脚本逻辑
   - 保留定时触发 (cron) 和手动触发 (workflow_dispatch)
   - 保留 Dry Run 模式支持

## Impact

### 受影响的规范
- 新增: `version-monitor` 规范

### 受影响的代码
- 新增: `nukeBuild/Build.Targets.VersionMonitor.cs`
- 修改: `nukeBuild/Adapters/AzureBlobAdapter.cs` (添加 `GetAllVersions` 方法)
- 修改: `nukeBuild/Build.cs` (添加 `GitHubToken` 参数和 `DryRun` 参数)
- 修改: `.github/workflows/version-monitor.yml` (使用 Nuke 目标)
- 修改: `.env.example` (添加 GitHub Token 相关配置)

### 操作影响
- 使用方式: `nuke VersionMonitor --AzureBlobSasUrl "<url>" --GitHubToken "<token>"`
- 可以与其他目标组合: `nuke VersionMonitor Download --Version v1.0.0`
- 本地测试更方便，无需 GitHub Actions 环境
