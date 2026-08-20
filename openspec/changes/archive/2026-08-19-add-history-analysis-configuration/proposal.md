## Why

Release Architecture Forensics needs one policy-backed, deterministic source for
the values that its future scoring stages may tune. The existing ingest pipeline
already implements the fixed Git and TaskKey semantics, but has no way to obtain
bounded extractor, path-category, ignore, weight, or threshold settings from the
normal architecture-policy lifecycle.

## What Changes

- Add an optional `history_analysis` section to the architecture-policy model,
  schema, raw-policy validation, and imported-policy composition lifecycle.
- Define bounded task extractors that feed the existing TaskKey extraction seam
  without changing canonical identity, byte-span provenance, normalization,
  ordering, deduplication, or overlap handling.
- Add deterministic exact-path category classification and pre-normalization
  ignore filtering, with `unknown` retained as a visible category.
- Add validated, exact-decimal hotspot/co-change/bottleneck/OCP profiles and
  the co-change significance threshold for downstream analysis stages.
- Keep the default effective configuration behavior-compatible when
  `history_analysis` is omitted, and reject invalid settings rather than
  repairing or silently accepting them.

## Capabilities

### New Capabilities

<!-- None. -->

### Modified Capabilities

- `release-architecture-forensics`: define the policy-backed bounded
  configuration surface consumed by ingestion and later scoring stages.

## Impact

The change affects the Core policy document, raw YAML/schema validation,
history-task extraction, history ingest command policy loading, future-analysis
configuration models, and focused Core/CLI tests. It adds no new executable,
Git backend, package, or configuration file authority.
