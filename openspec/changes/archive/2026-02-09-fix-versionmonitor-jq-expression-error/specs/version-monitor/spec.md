## MODIFIED Requirements

### Requirement: Version Comparison

系统 SHALL 比较 Azure Blob Storage 中的版本与 GitHub Releases 中已发布的版本，识别未发布的新版本。

#### Scenario: 发现新版本
- **WHEN** Azure 中存在 GitHub Releases 中未发布的版本
- **THEN** 系统应识别出新版本列表
- **AND** 版本比较应忽略 'v' 前缀差异

#### Scenario: 无新版本
- **WHEN** 所有 Azure 版本都已存在于 GitHub Releases 中
- **THEN** 系统应报告无新版本
- **AND** 不应触发发布流程

#### Scenario: GitHub API 调用成功
- **WHEN** GitHub CLI 调用 `gh release list` 命令且认证有效
- **THEN** 系统应正确获取所有现有 releases 的 tagName 列表
- **AND** jq 表达式 `'[[].tagName'` 应正确解析输出
- **AND** 返回的列表应包含所有版本标签

#### Scenario: GitHub API 调用失败
- **WHEN** GitHub API 调用失败（认证失败、网络错误等）
- **THEN** 系统应记录错误并退出
- **AND** 返回非零退出码
