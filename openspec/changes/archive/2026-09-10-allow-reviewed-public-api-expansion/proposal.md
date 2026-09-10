# Allow reviewed public API expansion

## Why

An intentional reviewed public API addition changes `resolved_snapshot_entries`, which currently fails closed as unproven policy weakening even when the regular public API workflow proves the exact change.

## What changes

- Add an explicit JSON approval bound to base/current policy-context digests, one public API contract, and its exact added entries.
- Accept only canonical addition-only deltas; removals, signature changes, selector changes, and unrelated fact changes remain blocking.
- Surface accepted approvals in human, JSON, SARIF, debt-gate, CLI, and Testing API output.
