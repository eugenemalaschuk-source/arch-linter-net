## Context

The producer already emits a complete `architecture-health-report-evidence` envelope containing the selected validation receipts, waiver lifecycle records, external-evidence requirements, and the original product-owned publication horizon. The Core waiver evaluator defines the date comparison semantics, while the Python publisher currently reads only the frozen horizon and treats an elapsed value as unavailable. The publisher is intentionally not allowed to load policy, build assemblies, or derive a second Health result.

The trusted promotion action is version-pinned and runs repository-owned Python from its own immutable action root. The reusable workflow has no consumer checkout. Any new revalidation execution must retain that trust boundary and must work before either raw GitHub publication or Relay publication.

## Goals / Non-Goals

**Goals:**

- Provide one Core-owned temporal interpretation of serialized Health evidence.
- Make the refreshed result deterministic for an explicit UTC evaluation date.
- Bind the refreshed receipt to the exact source Health bytes, badge payload bytes, merged tree, and producer identity already proven by the provider resolver.
- Keep Python promotion as orchestration and transport validation only.
- Preserve the existing unavailable reason codes and raw/Relay adapter contracts.

**Non-Goals:**

- Re-running architecture analysis, policy parsing, assembly discovery, or Health/Gate projection.
- Re-evaluating findings, counts, external diagnostic contents, or canonical badge bytes.
- Adding a waiver-management workflow, a second waiver evaluator, a new Relay API, or cloud credentials.
- Changing release publication authority or silently rotating the reviewed action/workflow pins.

## Decisions

### 1. Use an explicit Core temporal receipt, not a publisher-side date calculation

The new internal Core revalidator reads only the serialized report-evidence fields needed to prove temporal validity. It carries forward the original canonical facts and evaluates the stored lifecycle records at a supplied `DateOnly`; only an originally active record can become expired because of the newer date. Stale, invalid, metadata-incomplete, malformed, inconsistent, and required-external-without-a-finite-horizon cases remain unassessable. The receipt uses `architecture-health-temporal-publication-receipt/v1` and contains stable reason codes plus the four binding digests.

The input bytes are hashed before parsing. The CLI requires the caller to supply the expected source Health digest, badge payload digest, exact merged-tree SHA, and producer-identity digest; a mismatch is an unassessable result. A producer identity digest is computed by the provider resolver from its already-verified workflow/check/run/job/artifact facts, so Core remains provider-neutral while the receipt is content-bound to the original producer.

An alternative was to reconstruct a new `ArchitectureHealthOutcome` and call the existing projector. That would either require rebuilding non-temporal facts or make serialized evidence deserialization a second Health path, so it is rejected.

### 2. Add a read-only `health revalidate-publication` CLI subcommand

The command accepts the bounded Health JSON member, binding values, an explicit UTC evaluation date, and an optional output path. It writes only the temporal receipt and does not require a policy, baseline, solution, build, or assembly. This keeps the trusted operation usable from the promotion action and makes the contract directly testable without GitHub.

An alternative was to add a hidden library entry point called directly by Python. That would couple the publisher to .NET assembly loading and would make the external action contract harder to audit; the CLI is a narrower, inspectable boundary.

### 3. Refresh only after provider provenance and exact payload validation

The provider resolver continues to prove the merged pull request, main/head tree equality, pinned workflow, required check, producer run/job, and artifact manifest before invoking temporal revalidation. It downloads the bounded evidence member and badge artifact, computes their exact content digests, computes the producer-identity digest from the verified facts, and invokes the trusted CLI only when the frozen horizon has elapsed. The Python layer validates the returned schema, state, identity fields, date, and finite horizon; it never interprets waiver records.

For a fresh horizon, the existing path remains metadata-only. For an elapsed horizon, a ready receipt replaces only the `EvidenceContext.semantic_horizon`; the original badge payload remains byte-identical. The same evidence context is then passed to the existing decision and adapter code, so GitHub raw and Relay share identical readiness semantics.

### 4. Build the trusted CLI from the pinned action source

The promotion workflow will make .NET 10 available to the promote job. The composite action will restore/build the CLI project under its own immutable action root and expose that fixed project to the Python resolver. The action never checks out or executes consumer files. The release inventory and immutable pins remain explicit release authority; rotating them to a commit containing this capability is a separate reviewed release step, not an implicit side effect of the implementation.

### 5. Keep Relay unchanged at the transport boundary

Relay already receives a product-owned `semantic_horizon` during prepare/publish/renew and rejects elapsed or invalid leases. The promotion layer will pass the refreshed Core receipt's horizon through that same contract and will include the binding in the local publication receipt. No Durable Object lifecycle or Cloudflare storage semantics need to change for this issue.

## Risks / Trade-offs

- [Risk] A malformed serialized evidence envelope could be accepted by a permissive parser. → Use strict schema/kind/state checks, required field/type checks, duplicate identity checks, and fail-closed reasons; test malformed and mismatch fixtures.
- [Risk] The revalidator could accidentally become a second Health evaluator. → Keep its input limited to report evidence, prohibit policy/build options in the command, and assert in tests that the original summary and payload are not changed.
- [Risk] Building the trusted CLI adds promotion latency and depends on the pinned action source containing the matching code. → Build only the reviewed action-root project, keep the action pin immutable, and make missing/incompatible tooling unavailable rather than falling back to Python semantics.
- [Risk] UTC midnight can occur during a promotion attempt. → The provider passes one explicit UTC evaluation date into the receipt, and the existing decision still compares the final horizon with its trusted current time; a boundary race fails closed.
- [Risk] Required external evidence currently has no finite reuse metadata. → Preserve `required_external_evidence_horizon_unknown` and do not invent a lease for that case.

## Migration Plan

1. Implement and test the Core receipt and CLI command.
2. Wire the provider resolver and action/workflow to invoke the trusted command and validate the receipt.
3. Run focused Core/CLI/Python/Relay contract tests, the repository acceptance gates, and OpenSpec validation.
4. Merge the implementation through the normal pull request process without publishing or rotating release pins.
5. During the owning release-preparation step, update and review the immutable workflow/action/package references, then run the first-party post-merge dogfood check for an exact merged-tree receipt.

## Open Questions

None.
