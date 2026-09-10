# Design: Harden reviewed public API live evidence

## Decision

Retain `PublicApiSnapshotDiffer` as the canonical API delta engine, but calculate the delta from the base context snapshot to a read-only capture of the current CLR surface. The current context's resolved snapshot entries must exactly equal that captured surface.

## Rationale

An approval artifact and context JSON are review inputs, not authority for the CLR metadata that is actually exported. Requiring equality between the current context snapshot and an independently captured surface prevents manually inserted entries from being approved.

## Fail-closed behavior

Missing, malformed, stale, duplicate, mismatched-contract, or mismatched-surface evidence does not suppress a finding. Multiple matching evidence records are also rejected. Capturing is read-only and reuses the existing public-API application service.
