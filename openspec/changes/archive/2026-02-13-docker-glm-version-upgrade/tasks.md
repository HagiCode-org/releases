## 1. 实现环境变量支持

- [ ] 1.1 在 `docker_deployment/docker-entrypoint.sh` 中定义默认值变量
  - 添加 `ZAI_SONNET_MODEL="${ZAI_SONNET_MODEL:-glm-4.7}"`
  - 添加 `ZAI_OPUS_MODEL="${ZAI_OPUS_MODEL:-glm-4.7}"`

- [ ] 1.2 更新 settings.json 生成逻辑，使用环境变量替换硬编码值
  - 将 `"ANTHROPIC_DEFAULT_SONNET_MODEL": "glm-4.7"` 改为 `"ANTHROPIC_DEFAULT_SONNET_MODEL": "${ZAI_SONNET_MODEL}"`
  - 将 `"ANTHROPIC_DEFAULT_OPUS_MODEL": "glm-4.7"` 改为 `"ANTHROPIC_DEFAULT_OPUS_MODEL": "${ZAI_OPUS_MODEL}"`

## 2. 验证与测试

- [ ] 2.1 验证 shell 脚本语法正确性
  - 运行 `bash -n docker_deployment/docker-entrypoint.sh`

- [ ] 2.2 测试默认值行为（不设置环境变量）
  - 确认 Sonnet 默认使用 `glm-4.7`
  - 确认 Opus 默认使用 `glm-4.7`

- [ ] 2.3 测试环境变量覆盖行为
  - 设置 `ZAI_SONNET_MODEL=glm-5` 时，确认 Sonnet 使用 `glm-5`
  - 设置 `ZAI_OPUS_MODEL=glm-5` 时，确认 Opus 使用 `glm-5`

## 3. 文档更新

- [ ] 3.1 更新 `openspec/project.md` 中的环境变量说明
  - 添加 `ZAI_SONNET_MODEL` 和 `ZAI_OPUS_MODEL` 到可选环境变量列表
