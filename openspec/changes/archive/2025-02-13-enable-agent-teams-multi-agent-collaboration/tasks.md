## 1. 实现环境变量支持

- [ ] 1.1 在 `docker_deployment/docker-entrypoint.sh` 中添加 Agent Teams 环境变量
  - 添加 `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS="${CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS:-1}"`
  - 默认值设为 `1` 以启用功能

- [ ] 1.2 更新 settings.json 生成逻辑，添加 Agent Teams 配置
  - 在 settings.json 中添加 `"CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS": "${CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS}"`

## 2. 验证与测试

- [ ] 2.1 验证 shell 脚本语法正确性
  - 运行 `bash -n docker_deployment/docker-entrypoint.sh`

- [ ] 2.2 测试默认行为（不设置环境变量）
  - 确认 `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` 默认为 `1`

- [ ] 2.3 测试显式禁用行为
  - 设置 `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=0` 时，确认配置为 `0`
  - 设置 `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=""` 时，确认配置为空

## 3. 文档更新

- [ ] 3.1 更新 `openspec/project.md` 中的环境变量说明
  - 添加 `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` 到可选环境变量列表
  - 说明默认启用，可通过设置 `0` 禁用

- [ ] 3.2 更新 `.env.example`（如需要）
  - 添加 Agent Teams 配置说明注释
