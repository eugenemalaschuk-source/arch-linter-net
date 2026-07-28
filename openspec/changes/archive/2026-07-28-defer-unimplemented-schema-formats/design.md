## Context

Review of the packaged-schema change found that three entries were generic placeholders rather than contracts validated against real output.

## Goals / Non-Goals

**Goals:** ship only immutable resources that match existing writer output.

**Non-Goals:** implement finding, cache, or profiling formats owned by #373, #365, and #374.

## Decisions

- Remove unimplemented format entries and resources from the 0.5.1 package registry.
- Keep their release-envelope decisions in the compatibility design but defer shipment until a writer and generated-sample validation exist.
- Model API snapshots as a machine-readable description of their line-oriented grammar, not as a false JSON instance schema.

## Risks / Trade-offs

- [A consumer expects all planned formats] → the registry is authoritative about what the installed tool actually supports; deferred formats remain explicitly documented as not yet shipped.
