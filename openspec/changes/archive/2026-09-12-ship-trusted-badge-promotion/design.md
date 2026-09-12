## Context

The reference repository currently keeps most trusted promotion logic inline in `.github/workflows/publish-architecture-health-badge.yml`. The Relay runtime and canonical disclosure/evidence validators now exist under `relay/` and the CLI, but consumer-facing publication still lacks one shared contract. The result must preserve the existing raw endpoint while making the trust-sensitive path reusable for private consumers and Relay-backed disclosure.

## Goals / Non-Goals

**Goals:**

- Make provenance resolution, artifact validation, promotion decisions, and redacted diagnostics transport-independent and directly testable.
- Provide a versioned reusable workflow/action boundary with approved configuration rather than consumer-selected producer inputs.
- Keep raw, Relay, and no-publication adapters thin and policy-bound.
- Support monotonic publication, bounded retries, and metadata-only renewal without a second architecture analysis.
- Exercise the contract with hostile artifact fixtures, wrong-provenance fixtures, race/retry fixtures, workflow security lint, and a synthetic consumer.

**Non-Goals:**

- Implementing Relay storage, public rendering, or CLI installer behavior owned by #828/#832/#835.
- Replacing the existing Health/Gate semantics or regenerating payloads in a transport layer.
- Publishing packages, deploying a Relay, changing release authority, or making #830 independently authorize #806.
- Supporting arbitrary providers, callback URLs, custom authenticated transports, or a generic workflow catalogue.

## Decisions

### 1. Use a repository-owned promotion library with narrow workflow adapters

Put the reusable resolver/validator/promoter and its tests in a dedicated repository-owned tool area rather than keeping JavaScript embedded in YAML. The workflow invokes the library with an approved configuration and immutable evidence; the reference workflow and reusable workflow share the same entry point. This prevents the reference dogfood path from silently becoming a second implementation.

An alternative was to duplicate the current `github-script` blocks in a new reusable workflow. That is rejected because fixes would diverge and the consumer could not test the transport-independent contract without GitHub orchestration.

### 2. Treat configuration as reviewed authority, not runtime input

Define a checked-in, schema-validated setup/registry document containing the approved workflow path and SHA, event/ref, required check, producer job, artifact contract, disclosure profile, adapter, and Relay identity. The reusable interface accepts only a reference to an approved configuration and bounded event context. It never accepts an arbitrary run, artifact, repository, or destination URL as a producer selector.

### 3. Keep evidence provenance separate from payload projection

The producer remains responsible for canonical Health and disclosure projection. The promotion layer verifies exact bytes, manifest binding, producer provenance, and the product-owned validity receipt; it does not calculate Health facts or repair payloads. The Relay adapter sends the minimum approved envelope and delegates CAS, lease, revocation, and read-time validity authority to the Relay.

### 4. Model publication as an explicit decision and adapter operation

Resolve evidence into a typed ready/unavailable decision with a fixed reason taxonomy, then dispatch to one of three adapters. `github-raw` keeps the current branch/endpoint semantics, `relay` performs challenge-bound OIDC publication, and `none` records no public destination. Every adapter receives the same immutable evidence and cannot broaden its authority through URL or string inputs.

### 5. Make renewal a revalidation path, not a scan path

The renewal workflow rechecks current producer identity, required gate, artifact retention, configuration/pin identity, and product-owned semantic horizon before a conditional write. It cannot call the architecture analyzer or synthesize a passing payload. A failed recheck publishes unavailable or leaves the Relay in its current fail-closed state according to the adapter contract.

### 6. Use bounded, fixed diagnostics and security-oriented fixtures

Diagnostics distinguish verification failure, authorization failure, and transport unavailable while excluding raw tokens, private IDs, paths, and URLs. Tests cover wrong repository/base/head/tree/attempt/job/check/artifact, hostile archives and JSON, direct pushes, unsupported ruleset shapes, race/ABA, retries, cancellation, expiry, revocation, and disclosure leakage. Workflow lint remains authoritative for permissions and action pins.

## Risks / Trade-offs

- [Risk] GitHub rules and checks APIs vary by plan and ruleset shape → [Mitigation] treat capability uncertainty as unavailable and cover every documented compatible shape in fixtures.
- [Risk] A reusable workflow still has privileged execution context → [Mitigation] no consumer checkout/execution, explicit minimal permissions, pinned actions, fresh scoped OIDC, and static workflow/security lint.
- [Risk] Existing raw publication behavior regresses during extraction → [Mitigation] retain legacy endpoint fixtures and run current publisher tests through the new adapter before replacing the inline workflow.
- [Risk] Renewal can accidentally become a hidden validity extension → [Mitigation] require a product-owned validity receipt and compare it with trusted time before any conditional write.
- [Risk] Shared workflow and release files overlap with #800/#801/#835 → [Mitigation] keep the reusable contract and one-writer integration boundary isolated; document exact shared-file ownership and defer release asset wiring to its owner.

## Migration Plan

1. Add the typed configuration, promotion library, adapter interfaces, fixtures, and focused tests.
2. Add the versioned reusable workflow/action and convert the reference publisher to call the shared path through the raw adapter.
3. Add Relay and no-publication adapter integration plus metadata-only renewal/recovery entry points.
4. Run workflow/security lint, unit/integration/conformance suites, and the synthetic consumer acceptance.
5. Publish the candidate through the reviewed release process; rollback is the previous pinned workflow reference and existing raw publisher branch, with no automatic publication or Relay mutation during preparation.
