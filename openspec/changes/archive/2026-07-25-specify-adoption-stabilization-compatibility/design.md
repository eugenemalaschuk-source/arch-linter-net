## Context

Story #354 contains a deliberately broad stabilization epic. Several P0/P1 slices have already shipped or are in progress, while P2 authoring, distribution, cache, profiling, and cancellation work remains open. The design must allow independent slice execution without allowing those slices to invent incompatible identities or formats.

The approved `analysis-build-state/v1` contract from #387 is reused, not duplicated. Existing 0.5.0-compatible behavior and shipped 0.5.1 correctness fixes are treated as factual inputs.

## Goals / Non-Goals

**Goals**
- Establish one release-level compatibility/version registry.
- Define stable identity, finding, lifecycle, output, cache, profiling, concurrency, cancellation, and support boundaries.
- Keep small-policy defaults simple.
- Preserve the three existing CLI numeric exit codes.
- Make downstream ownership explicit.
- Reserve one final max-depth consistency review.

**Non-goals**
- Implement every child feature in this change.
- Re-open the approved analysis/build-state fingerprint slice.
- Publish a Checkpoint A release.
- Add arbitrary plugins, macros, runtime DI inspection, application execution, or semantic data-flow analysis.
- Claim unimplemented public capabilities as shipped.

## Decisions

### 1. One compatibility envelope

Use `adoption-stabilization/v1` for the release-level registry and one public release, 0.5.1. Child formats retain independent versions because they have different compatibility lifecycles.

### 2. Exact format registry

Pin policy root/fragment v1, baseline document v2 with violation identity v1, API snapshot v1, finding v1, build-state v1, cache v1, profile v1, and the compatibility registry v1. Versioned packaged schema ids live under `/schema/0.5.1/`.

Rejected: using only repository-default-branch schemas. That is mutable and breaks offline/release-matched tooling.

### 3. Identity is structural, not presentational

Canonical finding identity uses family-appropriate project/assembly/type/member/configuration/TFM/occurrence fields. Expansion adds a separate source-instance key. Messages, paths, line numbers, timings, provider, and output destinations are evidence only.

Rejected: concatenated display strings or line numbers as identity.

### 4. Existing exit codes remain stable

Keep numeric 0/1/2 and add typed machine-readable completion categories. This preserves shell compatibility while allowing cancellation/output/preflight distinctions.

Rejected: adding many numeric codes in 0.5.1.

### 5. One normalized finding model

All adapters project from `finding/v1` with a closed typed `details.kind` union. Human text remains complete without TTY/color.

Rejected: adapter-specific semantic reconstruction from messages or generic fields.

### 6. Multi-sink syntax

Use repeatable validation-only `--report <format>=<destination>`. Preserve `--format`/`--json` as legacy one-sink validation forms and reject mixing them with `--report`. Existing command-specific `--output <path>` options for baseline/API artifact creation remain unchanged. Render and validate every file sink before changing destinations; replace each destination atomically where supported. Do not claim a global all-or-none transaction across independent paths/filesystems. A mid-commit failure returns typed partial-output evidence and never reruns validation.

Rejected: reusing the existing artifact `--output` option for a different meaning, multiple independent validation invocations, a shell-specific delimiter syntax, or an impossible cross-filesystem transactional guarantee.

### 7. Cache is opt-in and untrusted

No cache option means no persistent cache. `--cache auto` uses the platform user cache under the release/schema namespace; a path may be caller supplied. Fingerprint equality is input, not authorization.

Rejected: repository-local implicit writes and trusting CI-restored bytes as correctness evidence.

### 8. Measured optimization and bounded parallelism

Profile v1 records deterministic counters and phase measurements. #374 captures pre/post checkpoints. Default parallelism is `max(1, min(Environment.ProcessorCount, 4))`; sequential mode is supported and semantically equivalent.

### 9. Cancellation wins before publication

Cancellation is cooperative across all phases and prevents successful publication of snapshots, cache entries, profiles, baseline/API updates, or required outputs.

### 10. Final consistency is one max pass

Slices are designed/implemented independently. After they land, #355 performs one repository-wide max-depth reconciliation against #366 Checkpoint B. Any incompatible child blocks closure/release.

## Risks / Mitigations

- **Risk:** the contract over-specifies child implementation.  
  **Mitigation:** specify public equality, lifecycle, trust, and adapter boundaries; leave internal algorithms to child design changes.

- **Risk:** current implementations have documented limitations versus the target contract.  
  **Mitigation:** keep limitations explicit and require Checkpoint B reconciliation; do not silently weaken the target.

- **Risk:** future child PRs ignore the contract.  
  **Mitigation:** add slice references to issue threads and require the final issue/spec/manifest comparison.

- **Risk:** versioned schema ids require packaging changes.  
  **Mitigation:** #372 owns implementation and offline discovery; this change only fixes the compatibility requirement.
