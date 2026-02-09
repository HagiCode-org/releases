## ADDED Requirements

### Requirement: Release Workflow Feishu Notification

系统 SHALL 在 Release workflow 完成后向飞书发送通知。

#### Scenario: Release 成功通知
- **WHEN** Release workflow 成功完成
- **THEN** 系统应向飞书发送包含以下信息的通知:
  - 发布状态: ✅ 成功
  - 版本号 (从 git tag 或输入获取)
  - Docker Hub 推送状态
  - Azure ACR 推送状态
  - Aliyun ACR 推送状态
  - GitHub Release 链接
  - Workflow 运行链接

#### Scenario: Release 失败通知
- **WHEN** Release workflow 执行失败
- **THEN** 系统应向飞书发送包含以下信息的通知:
  - 发布状态: ❌ 失败
  - 版本号 (如果可用)
  - 失败的步骤名称
  - Workflow 运行链接 (用于查看日志)

### Requirement: Version Monitor Workflow Feishu Notification

系统 SHALL 在 Version Monitor workflow 完成后向飞书发送通知。

#### Scenario: Version Monitor 发现新版本
- **WHEN** Version Monitor workflow 发现新版本（Azure Blob Storage 中的版本与 GitHub Releases 不一致）
- **THEN** 系统应向飞书发送包含以下信息的通知:
  - 监控状态: ✅ 发现新版本
  - 新版本列表
  - 已触发的 release workflow 链接
  - Azure Blob Storage 来源

#### Scenario: Version Monitor 无新版本（不发送通知）
- **WHEN** Version Monitor workflow 完成，但未发现新版本（Azure 和 GitHub 版本一致）
- **THEN** 系统应**不发送**通知
- **AND** workflow 应正常完成，不触发通知 job

#### Scenario: Version Monitor 失败通知
- **WHEN** Version Monitor workflow 执行失败
- **THEN** 系统应向飞书发送包含以下信息的通知:
  - 监控状态: ❌ 失败
  - 错误信息摘要
  - Workflow 运行链接

### Requirement: Notification Reliability

通知发送失败 SHALL 不影响主要 workflow 的执行状态。

#### Scenario: 通知发送失败
- **WHEN** 飞书 webhook 调用失败
- **THEN** workflow 应继续完成，不应因通知失败而标记为失败
- **AND** 通知 job 应记录错误信息

### Requirement: Notification Timing

通知 SHALL 在主要 workflow job 完成后立即发送。

#### Scenario: 通知时机
- **WHEN** 主要 job (ubuntu-latest 或 monitor) 完成
- **THEN** 通知 job 应立即执行
- **AND** 使用 `if: always()` 确保无论成功失败都发送通知

### Requirement: Version Monitor Conditional Notification

Version Monitor 通知 SHALL 仅在有版本差异或失败时发送，避免通知噪音。

#### Scenario: 有版本差异时发送通知
- **WHEN** monitor job 检测到 Azure Blob Storage 和 GitHub Releases 之间存在版本差异
- **THEN** notify job 应执行并发送通知
- **AND** 通知应包含新版本信息

#### Scenario: 无版本差异时不发送通知
- **WHEN** monitor job 检测到 Azure Blob Storage 和 GitHub Releases 版本一致
- **THEN** notify job 应跳过执行
- **AND** 不发送任何通知

#### Scenario: Version Monitor 失败时发送通知
- **WHEN** monitor job 执行失败
- **THEN** notify job 应执行并发送失败通知
- **AND** 通知应包含错误信息

#### Scenario: 实现方式
- **THEN** monitor job 应输出一个状态变量（如 `has_new_versions: true/false`）
- **AND** notify job 应使用条件判断 `if: needs.monitor.outputs.has_new_versions == 'true' || needs.monitor.result == 'failure'`
