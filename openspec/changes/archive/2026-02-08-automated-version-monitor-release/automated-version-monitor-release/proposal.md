# Automated Version Monitor and Release

**Change ID**: `automated-version-monitor-release`
**Status**: ExecutionCompleted
**Created**: 2025-02-08

---

## Overview

自动化监控 Azure Blob Storage 上的新版本并自动触发发布流程，消除人工监控和手动触发的依赖。

当前 Hagicode Release 仓库需要手动推送版本标签才能触发发布流程。本提案引入定时监控机制，自动检测 Azure Blob Storage 上新增的版本并触发完整的发布工作流。

---

## Context

### Current State
- **发布触发方式**: 手动推送 Git 标签 (`v*.*.*`)
- **发布流程**: GitHub Actions → 下载 Azure 包 → 构建镜像 → 推送到多镜像仓库 → 创建 GitHub Release
- **源包托管**: Azure Blob Storage（通过 index 文件管理版本列表）

### Problem Statement
1. **手动触发依赖**: 每次发布都需要人工推送版本标签
2. **版本监控缺失**: 无法自动感知 Azure 上新增的版本
3. **人工协调成本**: 需要人工检查 Azure index 并手动触发发布

### Target State
- 定时任务自动监控 Azure index 文件
- 检测到新版本时自动触发发布流程
- 支持手动触发和配置化检查频率

---

## Scope

### In Scope
- 创建定时监控 GitHub Actions 工作流
- 扩展现有发布工作流支持 `repository_dispatch` 触发
- 实现 Azure index 与 GitHub Release 的版本对比逻辑
- 添加失败通知和日志记录

### Out of Scope
- Azure Blob Storage 索引文件格式变更（假设格式稳定）
- 发布前的包验证（后续优化）
- 复杂的版本过滤策略（后续优化）

---

## Proposed Solution

### Architecture

```
┌─────────────────┐
│ 定时监控任务    │
│ (每 4 小时)     │
└────────┬────────┘
         │
         ▼
    ┌────────────────┐
    │ 检查 Azure     │
    │ Index 文件     │
    └────────┬───────┘
             │
             ▼
      ┌──────────────────┐
      │ 对比 GitHub       │
      │ Releases          │
      └────────┬─────────┘
               │
         ▼────────▼
   发现新版本?  无新版本
    │              │
    ▼              ▼
触发发布流程    结束
    │
    ▼
┌─────────────────────┐
│ 发布工作流          │
│ - 创建 Release      │
│ - 下载包            │
│ - 构建镜像          │
│ - 推送镜像          │
│ - 上传资源          │
└─────────────────────┘
```

### Component 1: Version Monitor Workflow

**文件**: `.github/workflows/version-monitor.yml`

| 配置项 | 值 |
|--------|-----|
| 触发方式 | `schedule` (cron: `0 */4 * * *`) |
| 手动触发 | `workflow_dispatch` |
| 权限 | `contents: read`, `actions: write` |

**核心逻辑**:
```yaml
1. 从 Azure Blob Storage 下载 index 文件
2. 解析所有可用版本
3. 通过 GitHub API 获取现有 Release 列表
4. 对比差异（index 中存在但 Release 中不存在的版本）
5. 如果发现新版本，使用 repository_dispatch 触发发布工作流
```

### Component 2: Enhanced Publish Workflow

**文件**: `.github/workflows/hagicode-server-publish.yml` (修改)

| 新增能力 | 说明 |
|---------|------|
| **Repository Dispatch** | 接受来自监控工作流的事件 |
| **版本参数化** | 接受传入的 `version` 输入参数 |
| **自动创建 Release** | 如果 Release 不存在则自动创建 |

**触发条件**:
- 原有: `workflow_run` (版本标签推送)
- 新增: `repository_dispatch` (来自监控任务)

---

## Impact Assessment

### Technical Changes

| 组件 | 变更类型 | 说明 |
|------|----------|------|
| `.github/workflows/version-monitor.yml` | Add | 新增定时监控工作流 |
| `.github/workflows/hagicode-server-publish.yml` | Modify | 扩展触发方式和参数 |
| `.github/workflows/hagicode-server-publish.csproj` | Modify | 可能需要添加 Nuke Target |

### Benefits
- ✅ **零人工干预**: 新版本自动检测和发布
- ✅ **及时性**: 最迟 4 小时内发现并发布新版本
- ✅ **可靠性**: 消除人为错误和遗漏
- ✅ **轻量级**: 监控任务仅执行检测，不执行重型操作

### Risks & Mitigation

| 风险 | 缓解措施 |
|------|----------|
| Azure index 文件格式变化 | 使用健壮的解析逻辑，添加错误处理和日志 |
| 重复触发同一版本 | GitHub Release 作为幂等性保证，记录已发布版本 |
| 定时任务失败 | 添加失败通知（issue 或 Slack）和详细日志 |
| 误触发未完成版本 | 支持版本过滤规则（如跳过 pre-release 标签） |
| 并发触发冲突 | GitHub Actions 默认排队机制 |

---

## Success Criteria

### Functional
- [ ] 定时任务每 4 小时自动执行
- [ ] 正确解析 Azure index 文件中的版本列表
- [ ] 准确对比 GitHub Release 并识别新版本
- [ ] 新版本自动触发发布工作流
- [ ] 发布工作流正确处理传入的版本参数

### Non-Functional
- [ ] 监控任务执行时间 < 30 秒
- [ ] 失败时生成可操作的错误日志
- [ ] 支持 `workflow_dispatch` 手动触发
- [ ] 幂等性：同一版本不会重复发布

### Validation Steps
1. 在 Azure Blob Storage 上放置一个新版本（不创建 Release）
2. 等待定时任务触发或手动触发监控工作流
3. 验证检测到新版本并触发发布工作流
4. 验证发布成功完成（镜像推送、Release 创建）

---

## Alternatives Considered

### Alternative 1: Azure Event Grid + Webhook
**描述**: 使用 Azure Event Grid 监控 Blob 创建事件并触发 GitHub webhook

**优点**: 实时响应，无轮询开销

**缺点**:
- 需要额外配置 Azure 资源
- Webhook 安全性复杂
- 增加基础设施依赖

**选择理由**: 定时任务更简单、可靠，且 4 小时延迟可接受

### Alternative 2: Nuke Target 定时任务
**描述**: 在现有的 Nuke 构建脚本中添加监控逻辑

**优点**: 复用现有 Nuke 基础设施

**缺点**:
- 需要外部调度器（如 GitHub Actions 也需要）
- 增加构建脚本复杂度

**选择理由**: 独立工作流更清晰，职责分离

---

## Open Questions

1. **版本过滤策略**: 是否需要跳过 pre-release 版本？
   - **建议**: 初步不过滤，后续可通过配置添加

2. **失败通知方式**: 定时任务失败时如何通知？
   - **建议**: 创建 GitHub Issue 或使用 Slack（如果可用）

3. **检查频率**: 4 小时是否合适？
   - **建议**: 可通过 workflow_input 支持动态配置

---

## Timeline Estimate

| 阶段 | 任务 | 估时 |
|------|------|------|
| 实现 | 创建监控工作流 | 2-3 小时 |
| 实现 | 扩展发布工作流 | 1-2 小时 |
| 测试 | 端到端测试和调试 | 1-2 小时 |
| **总计** | | **4-7 小时** |

---

## References

- 现有发布工作流: `.github/workflows/hagicode-server-publish.yml`
- GitHub Actions: `repository_dispatch` 事件文档
- Azure Blob Storage: REST API 文档
