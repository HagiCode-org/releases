# Implementation Tasks

**Change ID**: `automated-version-monitor-release`
**Status**: ExecutionCompleted
**Last Updated**: 2025-02-08

---

## Task Breakdown

### Phase 1: Setup and Investigation

#### Task 1.1: Analyze Existing Publish Workflow
- [x] Read and understand `.github/workflows/hagicode-server-publish.yml`
- [x] Identify current trigger mechanisms and inputs
- [x] Document current release process steps
- [x] Identify required modifications for `repository_dispatch` support

**Dependencies**: None
**Estimated Time**: 30 minutes

---

#### Task 1.2: Research Azure Index File Format
- [x] Locate Azure Blob Storage index file URL in existing workflow
- [x] Download and analyze index file structure
- [x] Document version extraction logic (regex pattern)
- [x] Test parsing logic with sample data

**Dependencies**: None
**Estimated Time**: 30 minutes

---

### Phase 2: Version Monitor Workflow

#### Task 2.1: Create Version Monitor Workflow File
- [x] Create `.github/workflows/version-monitor.yml`
- [x] Configure `schedule` trigger with cron `0 */4 * * *`
- [x] Configure `workflow_dispatch` trigger for manual execution
- [x] Set required permissions (`contents: read`, `actions: write`)

**Dependencies**: Task 1.1, Task 1.2
**Estimated Time**: 20 minutes

---

#### Task 2.2: Implement Azure Index Fetch Logic
- [x] Add step to download Azure index file
- [x] Parse index file to extract version list
- [x] Add error handling for download failures
- [x] Log parsed versions for debugging

**Dependencies**: Task 2.1, Task 1.2
**Estimated Time**: 30 minutes

---

#### Task 2.3: Implement GitHub Release Comparison
- [x] Add step to fetch GitHub Releases via API
- [x] Extract existing version numbers from releases
- [x] Compare Azure versions vs GitHub releases
- [x] Filter for new/missing versions

**Dependencies**: Task 2.2
**Estimated Time**: 30 minutes

---

#### Task 2.4: Implement Dispatch Trigger Logic
- [x] Add conditional step to check for new versions
- [x] Use `gh` CLI or API to trigger `repository_dispatch`
- [x] Pass detected version(s) as payload
- [x] Add "no new versions" exit handling

**Dependencies**: Task 2.3
**Estimated Time**: 30 minutes

---

#### Task 2.5: Add Failure Notifications
- [x] Implement failure notification mechanism
- [x] Option A: Create GitHub Issue on failure
- [x] Option B: Log detailed error information
- [x] Add retry logic for transient failures

**Dependencies**: Task 2.4
**Estimated Time**: 20 minutes

---

### Phase 3: Enhance Publish Workflow

#### Task 3.1: Add Repository Dispatch Trigger
- [x] Add `repository_dispatch` trigger to existing workflow
- [x] Configure event type filter (if needed)
- [x] Define input schema for version parameter

**Dependencies**: Task 1.1
**Estimated Time**: 15 minutes

---

#### Task 3.2: Parameterize Version Handling
- [x] Extract version from event payload
- [x] Fallback to Git tag if not from dispatch
- [x] Validate version format
- [x] Use parameterized version in release steps

**Dependencies**: Task 3.1
**Estimated Time**: 30 minutes

---

#### Task 3.3: Add Auto-Release Creation
- [x] Check if Release exists for version
- [x] Create Release if not exists
- [x] Reuse existing Release creation logic
- [x] Ensure idempotency for repeated runs

**Dependencies**: Task 3.2
**Estimated Time**: 20 minutes

---

### Phase 4: Testing and Validation

#### Task 4.1: Unit Testing Version Detection
- [ ] Create test Azure index with known versions
- [ ] Mock GitHub API responses
- [ ] Test version comparison logic
- [ ] Verify edge cases (no versions, all released, etc.)

**Dependencies**: Phase 2 complete
**Estimated Time**: 30 minutes

---

#### Task 4.2: Integration Testing
- [ ] Manually trigger monitor workflow
- [ ] Verify Azure index fetch succeeds
- [ ] Verify GitHub API calls succeed
- [ ] Verify dispatch trigger works correctly

**Dependencies**: Phase 3 complete
**Estimated Time**: 30 minutes

---

#### Task 4.3: End-to-End Testing
- [ ] Deploy new version to Azure (without Release)
- [ ] Trigger monitor workflow
- [ ] Verify publish workflow is triggered
- [ ] Verify complete release succeeds
- [ ] Verify idempotency (re-run should skip)

**Dependencies**: Task 4.2
**Estimated Time**: 45 minutes

---

### Phase 5: Documentation and Cleanup

#### Task 5.1: Update Documentation
- [ ] Document new monitor workflow
- [ ] Update runbook with manual trigger instructions
- [ ] Add troubleshooting guide for common failures
- [ ] Document version format expectations

**Dependencies**: Task 4.3
**Estimated Time**: 30 minutes

---

#### Task 5.2: Code Review and Refinement
- [ ] Self-review workflow files
- [ ] Add comments for complex logic
- [ ] Optimize for performance and readability
- [ ] Remove any debug/test code

**Dependencies**: Task 5.1
**Estimated Time**: 20 minutes

---

## Task Summary

| Phase | Tasks | Total Estimate |
|-------|-------|----------------|
| Phase 1: Setup and Investigation | 2 | 1 hour |
| Phase 2: Version Monitor Workflow | 5 | 2.5 hours |
| Phase 3: Enhance Publish Workflow | 3 | 1 hour |
| Phase 4: Testing and Validation | 3 | 2 hours |
| Phase 5: Documentation and Cleanup | 2 | 1 hour |
| **Total** | **15** | **~7.5 hours** |

---

## Critical Path

The following tasks form the critical path - they must be completed in order:

1. **Task 1.1** → Analyze existing workflow (foundational)
2. **Task 2.1** → Create monitor workflow (blocks implementation)
3. **Task 2.4** → Implement dispatch trigger (blocks publish workflow changes)
4. **Task 3.1** → Add repository dispatch trigger (integration point)
5. **Task 4.3** → End-to-end testing (validation)

---

## Parallel Execution Opportunities

The following tasks can be executed in parallel:

- **Task 1.1** and **Task 1.2** (independent research)
- **Task 2.2** and **Task 2.3** (if structure is pre-defined)
- **Task 4.1** and documentation prep

---

## Definition of Done

Each task is considered complete when:
- [ ] Code is written and committed
- [ ] Local testing passes
- [ ] Error handling is implemented
- [ ] Comments document complex logic
- [ ] No obvious bugs or edge cases remain

The entire change is complete when:
- [ ] All tasks are done
- [ ] End-to-end test succeeds
- [ ] Documentation is updated
- [ ] Code is reviewed and approved
