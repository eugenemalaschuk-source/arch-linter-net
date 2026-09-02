## 1. Canonical CLI projection

- [x] 1.1 Extend the Badge command definition and handler with `badge architecture-health`, parsing only canonical Health/report-evidence policy inventory and preserving the legacy command; verify focused command-definition tests pass.
- [x] 1.2 Implement deterministic Health-to-Shields message/color/exit projection, including validated unassessable fallback behavior and no independent policy counting; verify NUnit tests cover healthy, debt, degrading, failing, unassessable, malformed, and repeatable inputs.
- [x] 1.3 Update CLI reference and README-facing badge explanations for the new command and legacy compatibility; verify documented examples and endpoint wording match the actual payload schema.

## 2. Immutable pull-request evidence

- [x] 2.1 Extend the read-only Architecture Coverage producer to generate the exact CLI badge JSON from canonical Health output; verify the workflow never calculates Health, ignore debt, rules, colors, or messages itself.
- [x] 2.2 Add the fixed-schema bounded badge manifest with repository, PR/base/head/tree/run, payload hash, and byte binding; verify focused workflow tests cover the expected artifact shape and producer isolation.

## 3. Trusted main publication

- [x] 3.1 Add the dedicated push-to-main badge publisher that resolves one merged PR and validates repository/base/merge/required producer/artifact context plus PR-head versus main Git-tree identity; verify fixture tests cover a valid squash-merge promotion and a different-tree rejection.
- [x] 3.2 Validate artifact bytes as inert fixed-shape UTF-8 JSON before publication, generate the explicit unassessable CLI fallback when evidence is unavailable, and publish fixed endpoint/metadata files to the automation-owned static branch; verify fixtures cover missing, stale, failed, expired, malformed, and mismatched evidence without retaining a ready payload.
- [x] 3.3 Enforce least-privilege, serialized fixed-path publication with no PR checkout, no recursive main trigger, no architecture matrix, and no Pages/MkDocs deployment; verify workflow security/static tests assert permissions, trigger, and prohibited actions.

## 4. Documentation and specification synchronization

- [x] 4.1 Replace the README workflow-status architecture label with the stable Architecture Health Shields endpoint and retain distinct Main quality/SonarCloud/Codecov labels; verify no README `Architecture policy` image points at `ci.yml/badge.svg`.
- [x] 4.2 Document the stable endpoint's proof, limits, fail-closed promotion, no ordinary-main Pages deployment, and legacy command boundary in public CLI/CI guidance; verify `make lint-docs` passes.
- [ ] 4.3 Synchronize proposal/spec/design/tasks with the completed behavior, archive `publish-architecture-health-badge`, and inspect the generated main specs; verify `openspec validate --all --strict` passes.

## 5. Validation

- [x] 5.1 Run focused CLI and publisher-fixture test families, `make fmt`, workflow lint/security checks, and directly implicated architecture lint; verify every command succeeds.
- [x] 5.2 Run the release/CI-authority validation scope, including `make acceptance` and documentation validation; verify any issue-related failure is fixed and the final working tree contains only intended files.
