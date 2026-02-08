## 1. Implementation

- [ ] 1.1 修复 `GetGitHubReleases` 方法中的命令参数格式
  - 将 `Arguments` 属性替换为 `ArgumentList` 以正确传递带空格的参数
  - 确保 jq 表达式 `'[[].tagName'` 作为独立的参数项传递

- [ ] 1.2 添加命令调试日志
  - 在执行 `gh release list` 命令前记录完整的命令字符串
  - 便于故障排查和验证

- [ ] 1.3 本地验证修复
  - 使用 DryRun 模式测试 VersionMonitor 目标
  - 确认 GitHub releases 能正确获取

- [ ] 1.4 提交修复并验证 CI/CD 工作流
  - 触发 version-monitor.yml 工作流
  - 验证完整的版本监控流程

## 2. Testing

- [ ] 2.1 验证 GitHub releases 列表正确获取
  - 确认输出包含所有现有 releases
  - 验证版本格式正确

- [ ] 2.2 验证版本比较逻辑
  - 确认能正确识别新版本
  - 验证 'v' 前缀处理

- [ ] 2.3 验证错误处理
  - 确认证证失败时正确报错
  - 确认网络错误时正确处理
