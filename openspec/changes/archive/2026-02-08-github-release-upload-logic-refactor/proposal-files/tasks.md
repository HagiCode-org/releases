# Implementation Tasks

## 1. Code Implementation

- [x] 1.1 Replace `gh release create` command with `gh release upload` in `CreateGitHubRelease` method
- [x] 1.2 Remove `--title` and `--notes` parameters from the command (no longer needed for upload)
- [x] 1.3 Add `--clobber` flag to enable overwriting existing assets
- [x] 1.4 Update command structure to use `gh release upload {Version} {packageArgs} --repo {repository} --clobber`
- [x] 1.5 Update error handling and logging messages to reflect "upload" instead of "create" semantics
- [x] 1.6 Verify `GH_TOKEN` environment variable is still passed correctly for authentication

## 2. Testing

- [x] 2.1 Run `openspec validate github-release-upload-logic-refactor --strict` to verify proposal structure
- [ ] 2.2 Test locally with a pre-existing GitHub release to verify upload works
- [ ] 2.3 Verify `--clobber` flag behavior when uploading duplicate assets
- [ ] 2.4 Confirm error handling when release does not exist (should fail consistently with current behavior)
- [x] 2.5 Run full Nuke build locally to ensure no regressions in other targets

## 3. Documentation

- [x] 3.1 Update inline code comments to reflect "upload" semantics instead of "create"
- [x] 3.2 Update summary comment on `partial class Build` GitHub target if needed
- [x] 3.3 Verify `openspec/project.md` documentation remains accurate after changes

## 4. Validation

- [x] 4.1 Run `openspec validate github-release-upload-logic-refactor --strict` to ensure proposal passes validation
- [x] 4.2 Confirm all checkboxes in this file are completed before implementation approval
- [x] 4.3 Request review and approval before proceeding with code changes
