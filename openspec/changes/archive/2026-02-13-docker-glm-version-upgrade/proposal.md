# Change: Add Environment Variable Configuration for GLM Model Versions

## Why

Docker 镜像当前的 GLM 模型版本（Sonnet/Opus 层默认为 `glm-4.7`）是硬编码的，无法在不重新构建镜像的情况下进行修改。用户需要灵活地选择使用不同版本的 GLM 模型（如 `glm-5`），同时保持向后兼容性。

## What Changes

- 添加两个可选环境变量 `ZAI_SONNET_MODEL` 和 `ZAI_OPUS_MODEL`，允许用户覆盖默认的 GLM 模型版本
- **默认值保持 `glm-4.7` 不变**，确保向后兼容
- 用户可通过设置环境变量来使用 `glm-5` 或其他版本

### 新增环境变量

| 变量名 | 用途 | 默认值 |
|--------|------|--------|
| `ZAI_SONNET_MODEL` | Sonnet 层模型版本 | `glm-4.7` |
| `ZAI_OPUS_MODEL` | Opus 层模型版本 | `glm-4.7` |

### 使用示例

```bash
# 使用默认值 (GLM 4.7)
docker run -e ZAI_API_KEY=xxx hagicode

# 升级到 GLM 5
docker run -e ZAI_API_KEY=xxx \
  -e ZAI_SONNET_MODEL=glm-5 \
  -e ZAI_OPUS_MODEL=glm-5 \
  hagicode

# 仅升级 Sonnet 层
docker run -e ZAI_API_KEY=xxx \
  -e ZAI_SONNET_MODEL=glm-5 \
  hagicode
```

## Impact

- Affected specs: `docker-deployment`
- Affected code: `docker_deployment/docker-entrypoint.sh` (第 106-108 行)
- **向后兼容**：默认行为不变，现有部署无需修改
- **用户可选择升级**：通过环境变量灵活选择模型版本
