## 1. Release story scope format

- [x] 1.1 Define and document the versioned machine-readable scope block using the existing declaration fields and item semantics.
- [x] 1.2 Add fixtures for valid, missing, duplicate, malformed, mismatched, and cross-inventory-duplicate scope blocks.

## 2. Declaration generator

- [x] 2.1 Implement strict parsing and validation of the named release story through `gh` without inferring scope from milestones, labels, commits, or live issue state.
- [x] 2.2 Implement deterministic rendering of the existing v2 declaration schema and derive the output path only from the validated target under `tools/release/scopes/`.
- [x] 2.3 Add read-only preview and explicit write operations; make identical writes idempotent and reject conflicting existing declarations without modifying them.
- [x] 2.4 Add focused tests for canonical rendering, fixed-path confinement, preview non-mutation, atomic new-file creation, idempotence, and conflict preservation.

## 3. Regression coverage

- [x] 3.1 Replace the routine hard-coded full target inventory with generic checks for valid declarations and unique exact targets while retaining focused assertions for historically important release decisions.
- [x] 3.2 Prove that adding a declaration generated from a fixture requires no second per-release target/test transcription.

## 4. Release preparation integration

- [x] 4.1 Update `docs/ai/release-preparation-workflow.md` to author the structured block in the existing release story, run preview/generation, and include the generated declaration in the existing preparation PR.
- [x] 4.2 Document that the tracked declaration and exact candidate hash binding remain publication authority; keep the manual dry-run and separate publication procedure unchanged.
- [x] 4.3 Review the diff to confirm `.github/workflows/release-nuget.yml`, publication inputs, Checkpoint B, provenance, package outputs, and publication order are unchanged.

## 5. Validation

- [x] 5.1 Run focused generator tests and the complete `python -m pytest tools/release/tests/` suite using the repository's configured interpreter.
- [x] 5.2 Run `openspec validate --all` and verify all existing release declarations still pass the unchanged publication authorization consumer.

## Validation record

- Focused generator and release-authorization tests: 71 passed.
- Full `tools/release/tests/` suite: 478 passed, 17 failed. Existing Windows-incompatible harnesses account for the failures: publisher tests prepend fake `gh` with a POSIX `PATH` separator and Windows resolves the installed `gh.exe`; workflow-subject tests call the WSL `C:\Windows\System32\bash.exe` shim; two history tests cannot delete a read-only Git object or launch a POSIX shebang script as a Windows process. Launching pytest from Git Bash produced the same result.
- `openspec validate --all`: 175 passed.
- `git diff --check`: passed. The release workflow, authorization consumer, and publication procedure were not modified.
