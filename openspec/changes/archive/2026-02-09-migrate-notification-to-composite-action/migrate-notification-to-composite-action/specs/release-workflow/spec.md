## MODIFIED Requirements

### Requirement: Release Workflow 飞书通知

Release 工作流结束时，系统 SHALL 通过飞书 webhook 通知团队发布结果。

通知 SHALL 使用 Composite Action 方式调用 `HagiCode-org/haginotifier@v1`。

通知 step SHALL 配置为在主 job 内执行，使用 `if: always()` 确保无论发布成功或失败都发送通知。

通知配置 SHALL 使用 `env` 传递 `FEISHU_WEBHOOK_URL` 环境变量。

通知 step SHALL 设置 `id: feishu-notify` 以便访问输出参数。

通知输出 SHALL 使用 `steps.feishu-notify.outputs.*` 语法访问。

#### Scenario: 发布成功时发送通知
- **WHEN** Release 工作流执行成功
- **THEN** 发送飞书通知包含成功状态、版本号、触发者信息
- **AND** 通知标题为 "Hagicode Release 发布成功 ✅"
- **AND** 通知包含 GitHub Release 链接和 Workflow Run 链接

#### Scenario: 发布失败时发送通知
- **WHEN** Release 工作流执行失败
- **THEN** 发送飞书通知包含失败状态和错误信息
- **AND** 通知标题为 "Hagicode Release 发布失败 ❌"
- **AND** 通知包含 Workflow Run 链接用于排查问题

#### Scenario: 通知发送失败不影响主流程
- **WHEN** 飞书 webhook 调用失败
- **THEN** 通知 step 标记为失败但不影响工作流整体状态
- **AND** 工作流结果以 Release job 结果为准

### Requirement: Docker Build Workflow 飞书通知

Docker Build 工作流结束时，系统 SHALL 通过飞书 webhook 通知团队构建验证结果。

通知 SHALL 使用 Composite Action 方式调用 `HagiCode-org/haginotifier@v1`。

通知 step SHALL 配置为在主 job 内执行，使用 `if: always()` 确保无论构建成功或失败都发送通知。

#### Scenario: Docker 构建成功时发送通知
- **WHEN** Docker Build 工作流执行成功
- **THEN** 发送飞书通知包含成功状态
- **AND** 通知标题为 "Docker Build 构建成功 ✅"
- **AND** 通知包含 Workflow Run 链接

#### Scenario: Docker 构建失败时发送通知
- **WHEN** Docker Build 工作流执行失败
- **THEN** 发送飞书通知包含失败状态
- **AND** 通知标题为 "Docker Build 构建失败 ❌"

### Requirement: Version Monitor Workflow 飞书通知

Version Monitor 工作流结束时，系统 SHALL 通过飞书 webhook 通知团队版本监控结果。

通知 SHALL 使用 Composite Action 方式调用 `HagiCode-org/haginotifier@v1`。

通知 SHALL 仅在发现新版本或监控失败时发送。

通知 step SHALL 在监控 job 内执行，使用 `if: always()` 配合条件判断。

#### Scenario: 发现新版本时发送通知
- **WHEN** Version Monitor 检测到新版本
- **THEN** 发送飞书通知包含新版本信息
- **AND** 通知标题为 "✨ 发现新版本"
- **AND** 通知消息包含新版本列表

#### Scenario: 监控失败时发送通知
- **WHEN** Version Monitor 执行失败
- **THEN** 发送飞书通知包含失败状态
- **AND** 通知标题为 "❌ Version Monitor 失败"
- **AND** 通知消息提示检查日志

#### Scenario: 无新版本时不发送通知
- **WHEN** Version Monitor 执行成功但无新版本
- **THEN** 不发送飞书通知

### Requirement: 通知 Action 版本管理

通知 Composite Action SHALL 使用语义化版本引用。

系统 SHALL 使用 `@v1` 引用 `HagiCode-org/haginotifier`。

版本更新 SHALL 需要明确修改工作流文件中的版本号。

#### Scenario: 使用主版本引用
- **WHEN** 引用 haginotifier Action
- **THEN** 使用 `@v1` 而非 `@main`
- **AND** 确保获得 `v1` 系列的最新补丁版本

#### Scenario: 锁定特定版本
- **WHEN** 需要锁定特定行为
- **THEN** 可使用 `@v1.0.0` 精确版本
- **AND** 需要手动更新才能升级版本
