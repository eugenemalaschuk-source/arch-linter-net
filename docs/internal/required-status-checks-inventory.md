# Required status checks inventory

The active `Main` ruleset (`18790669`) has 15 required status contexts, verified on 2026-10-29. This quality change adds the trusted Qodana publisher context `qodana` to the versioned payload, for a total of 16. The active ruleset is not updated by this repository change: first merge the publisher workflow so GitHub can run the `workflow_run` workflow from the default branch, then apply the payload and verify enforcement.

The payload at [main-ruleset-required-status-checks.json](main-ruleset-required-status-checks.json) preserves the existing `Main` rules and adds only the `qodana` context. After the publisher workflow is present on the default branch, apply it with repository ruleset administration access:

```bash
gh api --method PUT repos/eugenemalaschuk-source/arch-linter-net/rulesets/18790669 \
  --input docs/internal/main-ruleset-required-status-checks.json
```

Then verify that the active ruleset contains all 16 contexts and confirm that a missing or failed `qodana` status blocks a test pull request. The report publisher must be present on the default branch before this setting is applied; otherwise GitHub cannot trigger it and the required status would remain missing.

## Required contexts

| Context | Workflow | Purpose |
|---|---|---|
| `Workflow Quality` | CI | actionlint, zizmor and workflow formatting |
| `Repository Lint` | CI | repository lint gates |
| `Coverage + Sonar` | CI | coverage collection plus trusted Sonar/Codecov upload where credentials are available |
| `qodana` | Qodana Gate Publisher | trusted differential Qodana result on the exact PR head |
| `Architecture Coverage` | CI | architecture coverage gate |
| `Tooling / Support Tests` | CI | Python tooling suites |
| `Unit Test Suite (Windows / Core Shard 1)` | CI | Windows Core test shard 1 |
| `Unit Test Suite (Windows / Core Shard 2)` | CI | Windows Core test shard 2 |
| `Unit Test Suite (Apple Silicon macOS / Core Shard 1)` | CI | macOS Core test shard 1 |
| `Unit Test Suite (Apple Silicon macOS / Core Shard 2)` | CI | macOS Core test shard 2 |
| `E2E Test Suite (Windows)` | CI | Windows end-to-end tests |
| `E2E Test Suite (Apple Silicon macOS)` | CI | macOS end-to-end tests |
| `Packed Artifact Test Suite (Windows)` | CI | Windows packed-artifact tests |
| `Packed Artifact Test Suite (Apple Silicon macOS)` | CI | macOS packed-artifact tests |
| `Analyze C#` | CodeQL | security-extended C# analysis |
| `Validate NuGet Packages` | Package Validation | package set and packed-artifact validation |

`qodana` is the exact commit-status context published by the trusted default-branch reporter. The scanner job is separately named `Qodana Community .NET (scan)`; its success only means evidence collection completed and is not the finding-level gate.

## Informational or unavailable contexts

| Check | Treatment | Reason |
|---|---|---|
| `Main Badge Refresh` | Not required | Push-only job; it does not run for PRs. |
| `CodeQL` summary check | Not separately required | Alert-level enforcement is already provided by the ruleset `code_scanning` rule and the required `Analyze C#` job. |
| SonarCloud app check | Not separately required | The SonarCloud app report depends on a secret-backed upload. `Coverage + Sonar` remains required and runs its coverage work independently. |
| `codecov/patch` | Not required | External upload depends on a token unavailable to untrusted PR workflows. |

The existing ruleset also enforces minimum code coverage and code quality independently of this required-status list. Keep those rules unchanged when applying the payload.
