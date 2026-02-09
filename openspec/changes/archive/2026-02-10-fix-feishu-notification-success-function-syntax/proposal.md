# Change: Fix Feishu Notification success() Function Syntax Error

## Why

The GitHub Actions workflow validation is failing because the `success()` function is being used in an unsupported context. In GitHub Actions expression syntax, `success()` is only available in specific contexts like `if` conditions, but not in `with` parameter blocks where the workflow currently uses it (lines 116-117 of `.github/workflows/hagicode-server-publish.yml`).

This syntax error prevents the workflow from being validated and blocks the entire CI/CD pipeline from executing.

## What Changes

- **Replace `success()` function calls with `job.status` context variable** in the Feishu notification step
- **Line 116 (title parameter)**: Change `${{ success() && '发布成功 ✅' || '发布失败 ❌' }}` to `${{ job.status == 'success' && '发布成功 ✅' || '发布失败 ❌' }}`
- **Line 117 (message parameter)**: Change `${{ success() && '发布成功' || '发布失败' }}` to `${{ job.status == 'success' && '发布成功' || '发布失败' }}`

## Impact

- **Affected specs**: `ci-cd` (CI/CD pipeline notification functionality)
- **Affected code**: `.github/workflows/hagicode-server-publish.yml:116-117`
- **Risk**: Low - This is a syntax-only fix that restores the intended behavior of displaying release status in Feishu notifications
- **Verification**: Workflow should pass GitHub Actions validation after this change
