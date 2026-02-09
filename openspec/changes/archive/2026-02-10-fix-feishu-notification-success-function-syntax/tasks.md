## 1. Implementation

- [x] 1.1 Verify current workflow validation error by reviewing `.github/workflows/hagicode-server-publish.yml` lines 116-117
- [x] 1.2 Replace `success()` function with `job.status == 'success'` comparison in line 116 (title parameter)
- [x] 1.3 Replace `success()` function with `job.status == 'success'` comparison in line 119 (message parameter)
- [x] 1.4 Validate the workflow file syntax (GitHub Actions should auto-validate on commit)
- [ ] 1.5 Verify notification displays correct status for both success and failure scenarios

## 2. Testing

- [ ] 2.1 Trigger a test release via `workflow_dispatch` to validate the notification format
- [ ] 2.2 Confirm Feishu notification shows "发布成功 ✅" when release succeeds
- [ ] 2.3 Confirm Feishu notification shows "发布失败 ❌" when release fails (simulate by introducing a temporary error)
