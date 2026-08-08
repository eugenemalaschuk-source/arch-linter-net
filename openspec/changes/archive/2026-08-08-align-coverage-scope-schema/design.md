## Context

Coverage contracts use `scope` as a discriminant. Runtime validation already treats
project and assembly coverage as discovery-wide: their units come from project
discovery and assembly resolution, so `roots` has no supported meaning. The full
policy schema incorrectly requires discovery-root-shaped `roots` for both scopes.
This root schema is embedded and packed by `ArchLinterNet.Core`; the fragment
schema references its definitions, and it is used when a composed policy's
effective document is validated.

## Goals / Non-Goals

**Goals:**

- Make source and packaged schemas accept project and assembly coverage without
  `roots`, matching runtime validation.
- Reject `roots` for those scopes through the same `not` constraints used by the
  other non-rooted scope branches.
- Prove direct `policy check` and effective-schema composition agree for the five
  issue-scoped coverage variants: namespace, project, assembly, dependency edge,
  and rule input.

**Non-Goals:**

- Change coverage discovery, classification, exclusions, or strictness.
- Redesign coverage schemas or alter semantic-role coverage.
- Add a new abstraction or runtime validator.

## Decisions

### Keep runtime semantics authoritative

The runtime has explicit actionable diagnostics that reject `roots` for project and
assembly coverage. Updating the schema is the smallest correction and preserves the
discovery-wide semantics specified for those scopes. Changing runtime to consume
roots would introduce unsupported filtering behavior and weaken the guarantee that
all discovered production units are modelled.

### Encode rejection in each discriminated schema branch

The project and assembly `then` branches will remove `required: [roots]` and their
`roots` item definitions, then include `roots` in their existing `not.anyOf` lists.
This keeps the JSON Schema diagnostic path local to the selected scope and leaves
namespace's required `roots` shape untouched. The fragment schema already delegates
contract definitions to the corrected root schema, preserving parity before and
after composition.

### Test public validation paths and the packed artifact

Regression tests will use policy fixtures with all five issue-scoped variants,
validate valid project/assembly entries without roots, assert actionable rejection
when roots are added, and exercise an equivalent imported policy. Package tests
will inspect the freshly produced NuGet schema rather than only source files.

## Risks / Trade-offs

- **[Risk]** Source schemas pass while packed resources are stale. **Mitigation:**
  run a package-artifact test that reads the produced schema.
- **[Risk]** Schema diagnostics differ from runtime diagnostics. **Mitigation:**
  assert both paths reject roots and name the invalid field and scope.
- **[Risk]** The issue predates semantic-role coverage and calls out five scopes.
  **Mitigation:** scope the new matrix to the five named issue variants while
  retaining all existing semantic-role validation tests unchanged.

## Migration Plan

No policy migration is required for valid policies: remove the schema-forced roots
from project and assembly contracts. Policies that already use roots are invalid at
runtime today and will become consistently invalid at schema validation. Rollback is
the ordinary revert of the schema and regression tests.

## Open Questions

None.
