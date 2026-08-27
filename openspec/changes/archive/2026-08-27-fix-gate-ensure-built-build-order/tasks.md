## 1. Receipt-backed gate preparation

- [x] 1.1 Route ensured baseline verification through metadata-only preparation, post-build receipt refresh, ordinary receipt verification, and prepared-runner materialization; verify the focused Core build-state orchestration tests pass.
- [x] 1.2 Add a disposable stale-output `gate --ensure-built --no-restore` regression with an in-sync baseline; verify it succeeds and records the rebuilt artifact receipt.

## 2. Change verification and synchronization

- [x] 2.1 Run changed-project tests, formatting, the directly implicated architecture lint, and OpenSpec validation; verify each reports success.
- [x] 2.2 Synchronize the implementation with the OpenSpec delta and archive the completed change; verify the rebuilt main specification passes strict validation.
