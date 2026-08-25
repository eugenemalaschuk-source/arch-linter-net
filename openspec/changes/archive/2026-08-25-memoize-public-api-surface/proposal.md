## Why

Several public-API contracts can govern the same unchanged assembly in one analysis session. Each contract currently repeats the complete reflection scan of exported types and members, including the selector-safety path, despite needing the same base facts.

## What Changes

- Add a lazy, session-owned immutable exported-public-API surface index keyed by the resolved assembly object identity.
- Reuse the indexed exported entries and exported-type membership across strict/audit validation and the capture/diff/update/migrate paths when they share a session.
- Add deterministic focused regression evidence for one materialization per assembly per session, selector/no-selector isolation, and fresh materialization in a new session.
- Preserve all contract-specific filtering, snapshot comparison, ignores, selector safety, finding identity, ordering, and report behavior. No persistent or cross-process cache is added.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-session-indexes`: A session also reuses its immutable exported public-API surface facts per resolved assembly.

## Impact

Core-only internal changes to the analysis session, public-API surface scanner/checker, and focused Core tests. There are no new packages, configuration, CLI switches, persisted data, or intentional public API changes.
