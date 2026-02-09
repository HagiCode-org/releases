## 1. 准备工作

- [ ] 1.1 确认 `haginotifier` 仓库已发布 `@v1` 版本的 Composite Action
- [ ] 1.2 查看 `haginotifier` 文档，确认 Composite Action 的输入参数和输出格式
- [ ] 1.3 创建功能分支用于此迁移

## 2. 更新 hagicode-server-publish.yml

- [ ] 2.1 移除 `feishu-notify` job 定义
- [ ] 2.2 在 `ubuntu-latest` job 中添加通知 step
- [ ] 2.3 更新调用方式为 `uses: HagiCode-org/haginotifier@v1`
- [ ] 2.4 将 `secrets` 改为 `env` 传递 `FEISHU_WEBHOOK_URL`
- [ ] 2.5 添加 `id: feishu-notify` 以便访问输出
- [ ] 2.6 确认 `if: always()` 条件正确设置
- [ ] 2.7 验证工作流语法正确

## 3. 更新 docker-build.yml

- [ ] 3.1 在 `ubuntu-latest` job 中添加通知 step
- [ ] 3.2 使用 `uses: HagiCode-org/haginotifier@v1`
- [ ] 3.3 配置适当的成功/失败消息
- [ ] 3.4 使用 `env` 传递 `FEISHU_WEBHOOK_URL`
- [ ] 3.5 添加 `id: feishu-notify` 以便访问输出
- [ ] 3.6 确认 `if: always()` 条件正确设置
- [ ] 3.7 验证工作流语法正确

## 4. 更新 version-monitor.yml

- [ ] 4.1 移除 `feishu-notify` job 定义
- [ ] 4.2 在 `monitor` job 中添加通知 step
- [ ] 4.3 更新调用方式为 `uses: HagiCode-org/haginotifier@v1`
- [ ] 4.4 将 `secrets` 改为 `env` 传递 `FEISHU_WEBHOOK_URL`
- [ ] 4.5 添加 `id: feishu-notify` 以便访问输出
- [ ] 4.6 更新条件逻辑：使用 `job.status` 或 `success()`/`failure()` 替代 `needs.*.result`
- [ ] 4.7 更新输出引用：将 `needs.monitor.outputs.*` 改为直接访问或通过 steps 访问
- [ ] 4.8 确认 `if: always()` 条件正确设置
- [ ] 4.9 验证工作流语法正确

## 5. 本地验证

- [ ] 5.1 使用 `act` 或 GitHub Actions CLI 进行本地测试（如可用）
- [ ] 5.2 检查所有工作流文件的 YAML 语法
- [ ] 5.3 确认没有使用已废弃的 `needs.*.outputs.*` 引用

## 6. 提交变更

- [ ] 6.1 提交所有修改的工作流文件
- [ ] 6.2 创建 PR 并填写描述
- [ ] 6.3 请求代码审查

## 7. 测试验证

- [ ] 7.1 在 PR 分支手动触发 `hagicode-server-publish` workflow
- [ ] 7.2 验证飞书通知正确发送
- [ ] 7.3 在 PR 分支手动触发 `docker-build` workflow
- [ ] 7.4 验证飞书通知正确发送
- [ ] 7.5 在 PR 分支手动触发 `version-monitor` workflow
- [ ] 7.6 验证飞书通知正确发送
- [ ] 7.7 测试失败场景：验证失败时通知仍正常发送

## 8. 合并和监控

- [ ] 8.1 合并 PR 到 `main` 分支
- [ ] 8.2 监控后续实际运行的工作流
- [ ] 8.3 确认所有通知功能正常工作

## 9. 文档更新

- [ ] 9.1 更新项目 README 中关于通知的说明（如有）
- [ ] 9.2 更新相关开发文档

## 10. 回滚准备（备选）

- [ ] 10.1 准备回滚脚本或步骤
- [ ] 10.2 确认可以快速恢复到 Reusable Workflow 方式
