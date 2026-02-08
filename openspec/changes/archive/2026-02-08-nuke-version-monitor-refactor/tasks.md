# Implementation Tasks

## 1. 准备工作
- [ ] 1.1 阅读并理解当前的 `version-monitor.yml` 工作流实现
- [ ] 1.2 确认 GitHub API 所需的权限范围 (repository dispatch)
- [ ] 1.3 确认现有 `AzureBlobAdapter.cs` 的结构和接口

## 2. 扩展 AzureBlobAdapter
- [ ] 2.1 在 `AzureBlobAdapter.cs` 中添加 `GetAllVersions(PackageIndex? index)` 方法
- [ ] 2.2 添加 `GetAllVersions` 方法的 XML 文档注释
- [ ] 2.3 更新 `IAzureBlobAdapter` 接口以包含新方法

## 3. 创建 VersionMonitor 构建目标
- [ ] 3.1 创建 `nukeBuild/Build.Targets.VersionMonitor.cs` 文件
- [ ] 3.2 添加 `VersionMonitor` 目标声明和依赖项
- [ ] 3.3 实现版本获取逻辑（从 Azure index.json）
- [ ] 3.4 实现版本比较逻辑（Azure 版本 vs GitHub Releases）
- [ ] 3.5 实现 repository_dispatch 触发逻辑
- [ ] 3.6 添加 DryRun 模式支持
- [ ] 3.7 添加日志输出和错误处理

## 4. 更新 Build.cs 参数
- [ ] 4.1 添加 `DryRun` 参数（bool 类型，默认 false）
- [ ] 4.2 确认 `GitHubToken` 参数已存在且配置正确
- [ ] 4.3 添加 `GitHubRepository` 参数（如未存在）

## 5. 更新 GitHub Actions 工作流
- [ ] 5.1 修改 `.github/workflows/version-monitor.yml` 以调用 Nuke
- [ ] 5.2 配置必要的环境变量和密钥导入
- [ ] 5.3 保留定时触发 (cron) 配置
- [ ] 5.4 保留手动触发 (workflow_dispatch) 和 dry_run 选项
- [ ] 5.5 保留失败时创建 issue 的逻辑

## 6. 更新环境配置
- [ ] 6.1 在 `.env.example` 中添加 `GITHUB_TOKEN` 说明
- [ ] 6.2 在 `.env.example` 中添加 `GITHUB_REPOSITORY` 说明（如需要）
- [ ] 6.3 添加 `DRY_RUN` 环境变量说明（可选）

## 7. 创建 OpenSpec 规范
- [ ] 7.1 创建 `specs/version-monitor/spec.md` 文件
- [ ] 7.2 定义版本监控功能的需求
- [ ] 7.3 添加成功场景：发现新版本并触发发布
- [ ] 7.4 添加成功场景：无新版本时不触发
- [ ] 7.5 添加成功场景：Dry Run 模式
- [ ] 7.6 添加错误场景：Azure 访问失败
- [ ] 7.7 添加错误场景：GitHub API 调用失败

## 8. 测试验证
- [ ] 8.1 本地测试 `nuke VersionMonitor --DryRun true`
- [ ] 8.2 验证版本比较逻辑正确性
- [ ] 8.3 验证 Dry Run 模式不触发实际发布
- [ ] 8.4 测试 GitHub Actions 工作流手动触发
- [ ] 8.5 验证失败时创建 issue 的功能

## 9. 文档更新
- [ ] 9.1 更新 `openspec/project.md` 中的 GitHub Actions 工作流表
- [ ] 9.2 更新 `openspec/project.md` 中的 Nuke 目标表
- [ ] 9.3 添加 VersionMonitor 到项目结构描述中

## 10. 验证和清理
- [ ] 10.1 运行 `openspec validate nuke-version-monitor-refactor --strict`
- [ ] 10.2 修复任何验证错误
- [ ] 10.3 确保所有代码遵循项目代码风格（ImplicitUsings, Nullable）
- [ ] 10.4 确保所有文件保存正确
