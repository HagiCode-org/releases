# Version Monitor Specification

本规范定义了版本监控功能的需求，该功能监控 Azure Blob Storage 中的新版本并自动触发发布流程。

## ADDED Requirements

### Requirement: Version Discovery

系统 SHALL 从 Azure Blob Storage 的 index.json 文件中获取所有可用版本列表。

#### Scenario: 成功获取版本列表
- **WHEN** Azure Blob Storage index.json 可访问且包含有效数据
- **THEN** 系统应解析并返回所有版本号
- **AND** 版本号应按语义化版本排序

#### Scenario: Azure 访问失败
- **WHEN** Azure Blob Storage 访问失败（无效 SAS URL、网络错误等）
- **THEN** 系统应记录错误并退出
- **AND** 返回非零退出码

### Requirement: Version Comparison

系统 SHALL 比较 Azure Blob Storage 中的版本与 GitHub Releases 中已发布的版本，识别未发布的新版本。

#### Scenario: 发现新版本
- **WHEN** Azure 中存在 GitHub Releases 中未发布的版本
- **THEN** 系统应识别出新版本列表
- **AND** 版本比较应忽略 'v' 前缀差异

#### Scenario: 无新版本
- **WHEN** 所有 Azure 版本都已存在于 GitHub Releases 中
- **THEN** 系统应报告无新版本
- **AND** 不应触发发布流程

#### Scenario: GitHub API 调用失败
- **WHEN** GitHub API 调用失败（认证失败、网络错误等）
- **THEN** 系统应记录错误并退出
- **AND** 返回非零退出码

### Requirement: Release Triggering

系统 SHALL 对于每个新发现的版本，通过 GitHub repository_dispatch 事件触发发布工作流。

#### Scenario: 成功触发发布
- **WHEN** 发现新版本且 Dry Run 模式未启用
- **THEN** 系统应为每个新版本触发 repository_dispatch 事件
- **AND** 事件类型应为 "version-monitor-release"
- **AND** payload 应包含版本号

#### Scenario: Dry Run 模式
- **WHEN** Dry Run 模式启用（`--DryRun true`）
- **THEN** 系统应输出将要触发发布的版本列表
- **AND** 不应实际触发 repository_dispatch 事件

#### Scenario: 触发失败
- **WHEN** repository_dispatch 触发失败
- **THEN** 系统应记录错误
- **AND** 继续处理其他版本（如适用）

### Requirement: Error Handling

系统 SHALL 在发生失败时创建 GitHub Issue 进行通知。

#### Scenario: 工作流失败时创建 Issue
- **WHEN** 版本监控工作流失败
- **THEN** 系统应创建 GitHub Issue
- **AND** Issue 标题应包含时间戳
- **AND** Issue 应包含工作流运行链接

#### Scenario: Issue 创建失败
- **WHEN** GitHub Issue 创建失败
- **THEN** 系统应记录警告但不影响退出码
- **AND** 不应导致级联失败

### Requirement: Scheduling and Manual Trigger

系统 SHALL 支持定时执行和手动触发。

#### Scenario: 定时执行
- **WHEN** 系统按计划时间运行（每 4 小时）
- **THEN** 应自动执行版本监控
- **AND** 不应输出详细摘要到 GitHub Step Summary

#### Scenario: 手动触发
- **WHEN** 用户手动触发工作流
- **THEN** 系统应执行版本监控
- **AND** 应输出详细摘要到 GitHub Step Summary
- **AND** 应支持 Dry Run 选项

### Requirement: Logging and Reporting

系统 SHALL 提供清晰的日志输出和状态报告。

#### Scenario: 标准日志输出
- **WHEN** 系统正常运行
- **THEN** 应输出 Azure 版本列表
- **AND** 应输出 GitHub Releases 列表
- **AND** 应输出新版本列表（如有）

#### Scenario: 手动触发摘要
- **WHEN** 工作流被手动触发
- **THEN** 应在 GitHub Step Summary 中生成结构化报告
- **AND** 报告应包含 Azure 版本、GitHub Releases、新版本三部分
