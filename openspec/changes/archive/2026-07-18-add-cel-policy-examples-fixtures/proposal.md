## Why

Issue #169: CEL-backed contract families (semantic port/adapter boundaries, layout
conventions, CEL `when` predicates) are fully implemented and documented in prose,
but no loadable, tested fixture demonstrates them together. The existing
modular-monolith and Unity-client samples predate these families, so an AI agent
or user copying `docs/contracts/port-boundary.md` or `layout-conventions.md`
snippets has no proof those exact shapes load and validate. This turns CEL from
an internal expression engine into a demonstrated, testable architecture-governance
capability, per #169's acceptance criteria.

## What Changes

- Extend `samples/policies/imports/modular-monolith/` with a `catalog` bounded
  context and a `legacy-crm` bounded context (new fragments), and extend the
  existing `sales` fragment, to add:
  - `strict_port_boundaries`: Sales Application -> Catalog through an approved
    `CatalogPort` seam (passing), plus a forbidden direct Sales -> Catalog
    `DomainLayer`/`Adapter` reference (violating case, covered by a second
    fixture project/variant).
  - `strict_port_boundaries`: LegacyCRM using an `AntiCorruptionLayer` seam
    (passing) vs. a forbidden direct database/infrastructure reference
    (violating case).
  - `strict_layout_conventions`: Application `Services`/`Interfaces` folder
    pairing (concrete service + matching `I`-prefixed interface counterpart),
    including a missing-counterpart violation case.
  - One contract using an explicit CEL `when:` predicate with a violation (no
    such example exists in the repo today).
  - At least one contract moved to an `audit_*` group to keep both strict and
    audit coverage present across the sample.
- Extend `samples/policies/imports/unity-client/` with a `strict_layout_conventions`
  rule expressing the existing Runtime/Editor/Features classification as a
  layout convention (e.g. Editor-only types must not appear under a Runtime
  folder segment), with a passing and a violating fixture variant.
- Add a violating-case fixture project/assembly (or equivalent existing-pattern
  mechanism already used by `PortBoundaryContractTests.cs` /
  `LayoutConventionContractTests.cs`) so each new contract has both a passing
  and a violating scenario under test.
- Add acceptance tests (new file(s) under `tests/ArchLinterNet.Core.Tests/`,
  following the pattern of `ArchitecturePolicyImportAcceptanceTests.cs`) that
  load the extended samples through the production policy loader and assert:
  passing fixtures produce zero violations, violating fixtures produce the
  expected violation with correct diagnostic fields, strict findings fail
  validation while audit findings do not, and JSON/explain diagnostic output is
  covered for the CEL `when`-backed violation.
- Update `docs/contracts/port-boundary.md`, `docs/contracts/layout-conventions.md`,
  and `docs/ai/policy-authoring-guide.md` to reference the new sample fixture
  paths as the canonical, tested example instead of (or alongside) the existing
  freestanding prose snippet.

No breaking changes — this only adds fixtures, tests, and doc cross-references
to existing, unchanged contract families.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `sample-policy`: adds requirements that the modular-monolith example
  demonstrates semantic port-boundary and layout-convention contracts
  (including an anti-corruption-layer seam and a CEL `when`-backed rule) and
  that the Unity-client example demonstrates a layout-convention contract for
  its Runtime/Editor/Features classification, each with a passing and a
  violating case validated by acceptance tests.

## Impact

- `samples/policies/imports/modular-monolith/architecture/**` (new/edited
  fragments, no root wiring changes beyond new imports).
- `samples/policies/imports/unity-client/architecture/**` (edited fragment).
- `tests/ArchLinterNet.Core.Tests/` (new acceptance test file(s); possibly new
  fixture types/assemblies mirroring existing `PortBoundaryContractTests.cs`
  fixture patterns for violating-case type references).
- `docs/contracts/port-boundary.md`, `docs/contracts/layout-conventions.md`,
  `docs/ai/policy-authoring-guide.md` (doc cross-references only).
- No production `src/` code changes — all consumed contract families already
  ship in Core.
