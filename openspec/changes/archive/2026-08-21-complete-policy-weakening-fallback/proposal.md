## Why

The #119 review also requires that explicit empty-tolerance and changed typed
facts cannot silently disappear from a policy-weakening comparison. The
comparator needs a bounded fallback where no directional semantics are proven.

## What Changes

- Report an authored source expansion changing from required to
  `optional_empty` as semantic weakening.
- Report a changed typed contract fact without a supported directional rule as
  deterministic `impact_not_proven` evidence.
- Keep selector-derived facts on their existing selector comparison path, so
  the fallback cannot duplicate or invent membership impact.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `policy-weakening-guardrails`: Complete fail-closed handling for
  empty-tolerant source applicability and otherwise unsupported typed facts.

## Impact

Changes the Core comparison result and NUnit regression coverage only; it does
not load policies differently or infer architecture membership.
