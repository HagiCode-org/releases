# 实施任务清单

## 概述

本文档列出了实施"统一 Docker 镜像多仓库发布流程"提案所需完成的具体任务。

## 任务列表

### 阶段 1：准备阶段

- [ ] **T1.1 验证现有构建系统**
  - 运行 `./build.sh DockerBuild` 确保现有构建流程正常工作
  - 运行 `./build.sh DockerPush` 验证 Docker Hub 推送功能
  - 验证 GitHub Actions workflow 配置
  - 验收标准：所有现有目标可正常执行

### 阶段 2：实现多注册表推送逻辑

- [ ] **T2.1 添加环境变量参数**
  - 在 `nukeBuild/Build.cs` 中添加以下参数：
    - `AliyunAcrUsername` (阿里云 ACR 用户名)
    - `AliyunAcrPassword` (阿里云 ACR 密码)
    - `AliyunAcrRegistry` (阿里云 ACR 注册表地址，默认值：registry.cn-hangzhou.aliyuncs.com)
    - `AzureAcrUsername` (Azure ACR 用户名)
    - `AzureAcrPassword` (Azure ACR 密码)
    - `AzureAcrRegistry` (Azure ACR 注册表地址，默认值：hagicode.azurecr.io)
  - 所有密码参数应标记为 `[Secret]`
  - 验收标准：参数在 `./build.sh --help` 中可见

- [ ] **T2.2 添加多注册表辅助方法**
  - 在 `nukeBuild/Build.Helpers.cs` 中添加以下方法：
    - `PushToRegistry(string localImage, string targetRegistry, string username, string password, List<string> tags)` - 推送镜像到指定注册表
    - `TagImageForRegistry(string sourceImage, string targetRegistry)` - 为镜像添加注册表前缀标签
  - 方法应包含适当的错误处理和日志记录
  - 验收标准：方法编译通过，包含 Serilog 日志语句

- [ ] **T2.3 添加 DockerPushAzure 目标**
  - 在 `nukeBuild/Build.Targets.Docker.cs` 中添加 `DockerPushAzure` 目标
  - 目标依赖：`DockerBuild`
  - 要求：`AzureAcrUsername`, `AzureAcrPassword`, `AzureAcrRegistry`
  - 实现逻辑：
    1. 登录到 Azure ACR
    2. 为现有镜像添加 Azure ACR 标签
    3. 推送所有标签到 Azure ACR
  - 验收标准：`./build.sh DockerPushAzure` 成功推送镜像到 Azure ACR

- [ ] **T2.4 添加 DockerPushAliyun 目标**
  - 在 `nukeBuild/Build.Targets.Docker.cs` 中添加 `DockerPushAliyun` 目标
  - 目标依赖：`DockerBuild`
  - 要求：`AliyunAcrUsername`, `AliyunAcrPassword`, `AliyunAcrRegistry`
  - 实现逻辑：
    1. 登录到阿里云 ACR
    2. 为现有镜像添加阿里云 ACR 标签
    3. 推送所有标签到阿里云 ACR
  - 验收标准：`./build.sh DockerPushAliyun` 成功推送镜像到阿里云 ACR

- [ ] **T2.5 添加 DockerPushAll 目标**
  - 在 `nukeBuild/Build.Targets.Docker.cs` 中添加 `DockerPushAll` 目标
  - 目标依赖：`DockerPush`, `DockerPushAliyun`, `DockerPushAzure`
  - 执行逻辑：仅记录所有推送完成
  - 验收标准：`./build.sh DockerPushAll` 成功推送到所有注册表

### 阶段 3：更新配置文件

- [ ] **T3.1 更新 .env.example**
  - 在 `.env.example` 文件中添加新的环境变量：
    ```bash
    # Aliyun Container Registry Configuration
    ALIYUN_ACR_USERNAME=your_aliyun_acr_username
    ALIYUN_ACR_PASSWORD=your_aliyun_acr_password
    ALIYUN_ACR_REGISTRY=registry.cn-hangzhou.aliyuncs.com

    # Azure Container Registry Configuration
    AZURE_ACR_USERNAME=your_azure_acr_username
    AZURE_ACR_PASSWORD=your_azure_acr_password
    AZURE_ACR_REGISTRY=hagicode.azurecr.io
    ```
  - 为每个变量添加清晰的注释说明其用途
  - 验收标准：`.env.example` 包含所有新增变量及注释

- [ ] **T3.2 更新 Release 目标依赖**
  - 在 `nukeBuild/Build.cs` 中修改 `Release` 目标
  - 将依赖从 `DockerPush, GitHubRelease` 改为 `DockerPushAll, GitHubRelease`
  - 验收标准：`./build.sh Release` 执行所有推送目标

- [ ] **T3.3 重新生成 GitHub Actions workflow**
  - 运行 `./build.sh --generate-configuration GitHubActions_hagicode-server-publish --host GitHubActions`
  - 验证生成的 workflow 包含新的 secrets 导入
  - 更新 `.github/workflows/hagicode-server-publish.yml`
  - 验收标准：workflow 文件包含新的环境变量

### 阶段 4：测试和验证

- [ ] **T4.1 本地功能测试**
  - 使用测试环境变量运行 `./build.sh DockerPushAll`
  - 验证镜像在所有注册表中正确打标签
  - 验证推送失败时的错误处理
  - 验收标准：本地测试通过，所有目标可独立执行

- [ ] **T4.2 CI/CD 配置验证**
  - 在 GitHub 仓库中添加新的 secrets：
    - `ALIYUN_ACR_USERNAME`
    - `ALIYUN_ACR_PASSWORD`
    - `AZURE_ACR_USERNAME`
    - `AZURE_ACR_PASSWORD`
  - 验证 workflow 可访问新 secrets
  - 验收标准：GitHub Actions 配置完成

- [ ] **T4.3 端到端测试**
  - 创建并推送测试 tag (如 `v0.0.0-test`)
  - 验证 GitHub Actions workflow 成功执行
  - 验证镜像出现在所有三个注册表：
    - Docker Hub
    - Azure Container Registry
    - Alibaba Cloud Container Registry
  - 验收标准：端到端测试成功，镜像在所有注册表中可用

### 阶段 5：文档和清理

- [ ] **T5.1 更新项目文档**
  - 更新 README.md 说明新的发布流程
  - 添加环境变量配置说明
  - 验收标准：文档反映新的发布流程

- [ ] **T5.2 通知相关团队**
  - 通知 `pcode-docs` 仓库维护团队
  - 说明新的统一发布流程
  - 建议禁用或移除 `sync-docker-acr.yml` workflow
  - 验收标准：相关团队已收到通知

- [ ] **T5.3 清理和验证**
  - 验证代码符合项目编码规范
  - 检查是否有未使用的代码或配置
  - 提交所有更改到版本控制
  - 验收标准：代码库状态清洁，提交完成

## 执行顺序

```
阶段 1: 准备阶段
  └─ T1.1
       │
阶段 2: 实现多注册表推送逻辑
       ├─ T2.1 (添加环境变量参数)
       ├─ T2.2 (添加辅助方法)
       ├─ T2.3 (添加 DockerPushAzure)
       ├─ T2.4 (添加 DockerPushAliyun)
       └─ T2.5 (添加 DockerPushAll)
            │
阶段 3: 更新配置文件
       ├─ T3.1 (更新 .env.example)
       ├─ T3.2 (更新 Release 目标)
       └─ T3.3 (重新生成 workflow)
            │
阶段 4: 测试和验证
       ├─ T4.1 (本地功能测试)
       ├─ T4.2 (CI/CD 配置验证)
       └─ T4.3 (端到端测试)
            │
阶段 5: 文档和清理
       ├─ T5.1 (更新项目文档)
       ├─ T5.2 (通知相关团队)
       └─ T5.3 (清理和验证)
```

## 验收总结

完成所有任务后，应满足以下条件：

1. **功能性**
   - 所有推送目标可独立执行
   - `DockerPushAll` 推送到所有注册表
   - Release 流程包含所有镜像推送步骤

2. **配置性**
   - 环境变量完整且文档化
   - GitHub Actions workflow 配置正确
   - 本地开发和 CI/CD 环境均支持

3. **可靠性**
   - 单个注册表失败不影响其他注册表
   - 错误消息清晰明确
   - 日志记录完整

4. **可维护性**
   - 代码遵循现有模式
   - 文档更新完整
   - 相关团队已通知
