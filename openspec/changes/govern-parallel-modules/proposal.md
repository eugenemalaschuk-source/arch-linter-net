## Why

Removing today's partial god classes is necessary but does not prevent their return in a new
form: several independently developed features can still accumulate behaviour in a common
namespace, hand-maintained policy inventory, or reflection registration seam. We need a
default architecture in which a direct child module is owned and evolved independently, so
parallel work changes different files and cross-module coupling is intentional and reviewable.

## What Changes

- Add a module-container policy capability that discovers direct child modules of a declared
  namespace, verifies their internal shape, and prohibits direct sibling dependencies without a
  hand-maintained layer list.
- Establish a feature-first module scaffold for CLI commands and future domain contexts:
  implementation, abstractions, models, and exceptions have explicit ownership and dependency
  direction; optional layers appear only when a feature needs them.
- Strengthen recursive `Abstractions`, `Models`, and `Exceptions` conventions: abstractions are
  interface/abstract-type boundaries, models and exceptions are dependency leaves, and exception
  folders cannot conceal unrelated type kinds.
- Make cross-module collaboration use a narrowly named, owner-reviewed published contract or
  shared kernel; prohibit generic `Common`, `Shared`, or `Utils` behaviour buckets.
- Add deterministic scaffolding and architecture tests so a new module is governed immediately,
  while adding one does not require edits to a central list of peer modules.
- Preserve the current strict CLI command-independence contract during migration; make the
  discovered-module rule prove equivalent coverage before it becomes authoritative.

## Capabilities

### New Capabilities

- `module-container-contracts`: Discover and govern repeated, independent modules beneath one
  namespace container, including sibling isolation and exhaustive module-shape coverage.
- `module-scaffolding`: Create a minimal, policy-compliant module and focused test fixture without
  introducing a generic shared implementation area.

### Modified Capabilities

- `layout-convention-contracts`: Express role and modifier-aware folder purity for abstractions
  and exceptions, in addition to existing type-kind placement checks.
- `self-architecture-policy`: Apply the new modularity, convention, and anti-god-node rules to
  ArchLinterNet itself, with audit-to-strict migration and negative regressions.
- `cli-command-dispatch`: Restrict reflection-based command discovery to the governed module
  boundary so arbitrary types cannot become commands by accident.

## Impact

- Affects Core policy models, schema/validation, source-fact discovery, command dispatch, CLI
  scaffolding, self-policy YAML imports, architecture tests, and architecture documentation.
- The existing `decompose-god-classes` change remains the dependency for eliminating current
  partial aggregates; this change complements it and does not weaken its strict final rule.
- No public package API change is intended. Existing commands remain valid while their current
  handwritten inventory is checked in parallel with the new discovered-module contract.
