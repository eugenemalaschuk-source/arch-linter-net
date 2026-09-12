## 1. Shared promotion contract

- [x] 1.1 Add the repository-owned typed promotion model, approved configuration schema, reason taxonomy, and exact evidence/provenance interfaces; verify schema and invalid configuration tests reject arbitrary producer and destination inputs.
- [x] 1.2 Implement canonical artifact/archive safety and manifest/disclosure validation over bounded untrusted bytes; verify hostile path, symlink, duplicate, size, UTF-8, JSON, and digest fixtures fail closed.
- [x] 1.3 Implement the transport-independent ready/unavailable decision and monotonic generation/deadline/idempotency checks; verify wrong repository/base/head/tree/attempt/job/check/artifact, direct push, cancellation, retry, and ABA fixtures.

## 2. Approved adapters and renewal

- [x] 2.1 Implement the `github-raw`, `relay`, and `none` adapters against the shared contract; verify legacy public raw bytes remain compatible and private/raw or arbitrary-URL configurations return migration diagnostics.
- [x] 2.2 Wire Relay challenge-bound OIDC publication, recovery, invalidation, and conditional renewal using the registered identity and product-owned validity horizon; verify stale writer, revocation, expiry, provider uncertainty, and replay fixtures.
- [x] 2.3 Add metadata-only renewal and recovery entry points that never rerun main architecture analysis; verify valid same-tree renewal succeeds before the horizon and becomes unavailable after horizon, artifact deletion, or producer-pin change.

## 3. Reusable workflow/action integration

- [x] 3.1 Add the versioned reusable workflow/action and approved setup/compatibility configuration; verify a synthetic external private/public consumer can invoke the pinned candidate without copied verifier code.
- [x] 3.2 Replace the repository-specific trusted publisher path with the shared implementation and thin raw adapter while preserving the existing unavailable fallback; verify reference dogfood and existing publisher tests pass.
- [x] 3.3 Enforce least-privilege permissions, immutable action/project pins, fresh OIDC audience/workflow binding, no consumer checkout/execute path, bounded outputs, and redacted diagnostics; verify actionlint, zizmor, workflow formatting, and secret/path corpus checks.

## 4. Evidence, documentation, and release handoff

- [x] 4.1 Add conformance vectors and integration fixtures for supported branch-protection/ruleset forms, missing gates, wrong attempts, artifact retention, disclosure leakage, and transport races; verify focused promotion and Relay suites pass.
- [x] 4.2 Document approved configuration, adapter modes, renewal semantics, unsupported capability diagnostics, private/public disclosure boundaries, and the pinned consumer installation path; verify documented examples match the schema and workflow inputs.
- [x] 4.3 Record the exact reusable component/configuration identity and release handoff inventory for #825/#806 without publishing or introducing a competing release authority; verify the release-scope structure and no unrelated v0.9 references.

## 5. Validation and specification closure

- [x] 5.1 Run focused unit/integration/conformance tests for the shared promotion, adapters, Relay, and existing raw path; verify all issue-related failures are resolved.
- [x] 5.2 Run `make fmt`, relevant workflow/security lint, `openspec validate --all`, and the repository architecture/policy checks implicated by changed files; inspect the resulting diff for unrelated files and secrets.
- [x] 5.3 Synchronize and archive the OpenSpec change, then rerun `openspec validate --all` and confirm the archived specs describe the shipped behavior.
