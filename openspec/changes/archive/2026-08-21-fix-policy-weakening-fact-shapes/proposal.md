## Why

The #119 comparator currently chooses semantic direction from fact-name
prefixes. Policy-context facts with the same prefix have incompatible typed
shapes, which can create both false semantic failures and missed relaxations.

## What Changes

- Replace prefix-based direction with an explicit fact-name, shape, and
  direction matrix.
- Compare supported scalar-set inventories and known boolean prohibition flags
  semantically.
- Route scalar predicates and structured facts without a dedicated comparator
  to one bounded `impact_not_proven` finding.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `policy-weakening-guardrails`: Make fact comparison shape-aware and retain
  unsupported typed changes for review.

## Impact

Changes only the internal Core comparator, its regression tests, and user
guidance. No policy-context schema or public API changes are required.
