# Implementation Tasks

## 1. 准备工作

- [x] 1.1 创建飞书自定义机器人并获取 webhook URL
- [x] 1.2 在 hagicode-release 仓库中添加 `FEISHU_WEBHOOK_URL` secret

## 2. Release Workflow 飞书通知集成

- [x] 2.1 在 `hagicode-server-publish.yml` 中添加通知 job
- [x] 2.2 配置通知 job 的依赖关系（needs: ubuntu-latest）
- [x] 2.3 设置 `if: always()` 确保无论成功失败都发送通知
- [x] 2.4 实现飞书富文本消息格式
- [x] 2.5 添加版本号、发布状态、各镜像仓库推送结果等信息

## 3. Version Monitor Workflow 飞书通知集成

- [x] 3.1 在 `version-monitor.yml` 中添加通知 job
- [x] 3.2 配置通知 job 的依赖关系（needs: monitor）
- [x] 3.3 添加条件判断：仅在有版本差异时发送通知（使用 job outputs 传递版本差异状态）
- [x] 3.4 设置 `if: failure()` 确保失败时也发送通知
- [x] 3.5 实现飞书富文本消息格式
- [x] 3.6 添加监控结果、新版本发现情况等信息

## 4. 通知消息格式设计

- [x] 4.1 设计 Release 成功消息格式（包含版本号、各镜像仓库状态、GitHub Release 链接）
- [x] 4.2 设计 Release 失败消息格式（包含错误信息、日志链接）
- [x] 4.3 设计 Version Monitor 成功消息格式（包含是否有新版本、新版本列表）
- [x] 4.4 设计 Version Monitor 失败消息格式（包含错误信息）

## 5. 测试验证

- [ ] 5.1 使用 workflow_dispatch 测试 Release 通知（成功场景）
- [ ] 5.2 测试 Release 失败场景的通知（可模拟失败）
- [ ] 5.3 测试 Version Monitor 发现新版本时的通知
- [ ] 5.4 测试 Version Monitor 无新版本时不发送通知
- [ ] 5.5 测试 Version Monitor 失败场景的通知
- [ ] 5.6 验证飞书消息格式正确、链接可点击
- [ ] 5.7 验证 Version Monitor 在无版本差异时不会产生通知噪音

## 6. 文档更新

- [x] 6.1 更新 README.md 添加飞书通知配置说明
- [x] 6.2 在 openspec/specs/release-workflow/ 中创建相应规范（如果需要）

