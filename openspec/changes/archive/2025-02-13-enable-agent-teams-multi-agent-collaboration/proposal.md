# Change: Enable Claude Code Agent Teams Multi-Agent Collaboration

## Why

Claude Code 近期发布了 **Agent Teams** 功能，这是一种全新的多代理协作执行模式。当前项目配置中缺少对 Agent Teams 功能的环境变量支持，导致无法利用这一新特性来提升多角度调试、协作分析等复杂任务的效率。

与传统子代理模型不同，Agent Teams 允许 3-5 个独立的 Claude Code 实例在同一个项目上协作，共享上下文、交换消息，并通过共享任务系统进行协调。

## What Changes

- 添加 `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` 环境变量配置
- 在 `docker_deployment/docker-entrypoint.sh` 中启用该实验性功能
- 更新项目文档说明如何使用 Agent Teams

### Agent Teams 架构概览

| 组件 | 功能 |
|------|------|
| `TeamCreate` | 创建团队脚手架（在 `.claude/teams/` 下创建文件夹） |
| `TaskCreate` | 添加任务为 JSON 文件，包含状态跟踪、依赖和所有权 |
| `Task tool` (升级版) | 支持 `name` 和 `team_name` 参数激活团队模式 |
| `taskUpdate` | 代理用于认领任务、更新状态、标记完成 |
| `sendMessage` | 支持直接消息和广播，写入 `.claude/teams/<team_id>/inbox/` |

### 使用场景

**最佳用例**：深度调试多假设场景

示例：用户报告应用在一条消息后退出而非保持连接。可以生成多个代理队友分别调查不同理论，让他们互相交流、像科学辩论一样反驳彼此的观点，并更新发现文档形成共识。

### 推荐终端配置

- 使用 tmux 或 iTerm2 获得最佳体验
- 启动命令：`claude --teammate-mode tmux`
- 效果：团队领导一个窗格，每个代理队友单独窗格，可实时观察和交互

## Impact

- Affected specs: `claude-code-config`
- Affected code: `docker_deployment/docker-entrypoint.sh`
- **向后兼容**：默认启用该功能，不影响现有行为
- **用户可选择禁用**：通过设置环境变量为空或 `0` 来禁用
