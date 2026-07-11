## Context

`classification.attributes`/`classification.assembly_attributes` (#107) already
let a policy map a user-owned custom attribute to a role by full type name,
without any ArchLinterNet-provided assembly. The semantic-role-catalog change
(#172, archived as `2026-07-11-decide-first-wave-annotation-strategy`) already
decided the catalog's example annotation names (`DomainLayer`,
`SharedKernel`, etc.) ship as candidates/examples only, and explicitly
deferred "a separate product and packaging decision" for any optional
annotation package to this issue. That placeholder is still open.

## Goals / Non-Goals

**Goals:**

- Resolve the deferred packaging decision: state plainly whether ArchLinterNet
  ships a binary annotation package, a source-only annotation package, both,
  or neither in this wave.
- Document the trade-offs between the three adoption paths so policy authors
  can make an informed choice.
- Close the two spec placeholders that reference issue #108.

**Non-Goals:**

- No new project, NuGet package, source generator, or package-format
  implementation.
- No attribute-to-role extraction/inference implementation in the core
  runner — that is explicitly deferred to follow-up extraction work
  (#109-#114).
- No framework-specific annotation types.
- No change to the `classification` YAML schema — the mapping mechanism this
  decision relies on already exists per #107.

## Decisions

**Decision: Ship no annotation package (binary or source-only) in this wave;
user-defined attributes mapped by full type name remain the sole supported
adoption path.**

Rationale:

- No demonstrated need exists yet. Extraction itself (#109-#114) is not
  implemented, so no project currently depends on any annotation shape being
  stable — adding a package now would be speculative.
- A binary package would add a compile-time (and, depending on packaging, a
  potential runtime/dependency-graph) reference to every consuming project —
  exactly the outcome issue #108 rules out as non-default.
- A source-only package (e.g. a `.props`-embedded `.cs` file or a
  source-generator-only NuGet package) avoids the runtime assembly reference,
  but still introduces a versioned artifact ArchLinterNet must design, ship,
  and support compatibility for — a maintenance surface not justified without
  a concrete consumer need.
  A user-owned attribute costs the adopting project one small `.cs` file they
  already fully control, with zero dependency footprint and zero version
  coupling to ArchLinterNet's release cadence.
- This mirrors the project's default decision bias (prefer existing
  mechanisms over new abstractions; no speculative packaging without
  confirmed need) and is consistent with the first-wave catalog decision that
  already shipped zero built-in annotation types.

Alternatives considered:

- *Ship a binary `ArchLinterNet.Annotations` package now.* Rejected — directly
  contradicts the issue's acceptance criteria that a binary package must not
  be the default or required path, and there is no consumer demand yet to
  justify the maintenance cost.
- *Ship a source-only package now.* Rejected for this wave — lower risk than
  a binary package, but still speculative before any extraction consumer
  exists. Documented as a candidate for a future, separately-decided change
  if user demand for a copy-paste-free starting point emerges.
- *Leave the decision open/undocumented.* Rejected — this is exactly the gap
  issue #108 exists to close; leaving it open perpetuates the placeholder in
  `semantic-role-catalog/spec.md`.

**Decision: Document the trade-off explicitly in
`docs/policy-format/semantic-classification.md`, extending the existing
"reserved" doc rather than creating a new page.**

Rationale: the doc already carries the "No annotation package" bullet under
Current limits and the full `classification.attributes` YAML example;
readers evaluating adoption paths look there first. A new doc page would
fragment the single source of truth for this reserved feature area.

**Decision: Add compile-only fixtures demonstrating the recommended pattern,
with no scanner assertions.**

Rationale: the issue's validation section asks for "compile fixtures for
user-defined internal custom attributes" and fixtures "proving the extractor
can map attributes by configured full name." Since extraction is explicitly
out of scope (non-goal, above), the only fixture obligation this change can
honestly satisfy is a compile-only demonstration that the documented pattern
(an internal, `[AttributeUsage]`-annotated custom attribute) is valid C#, in
the same shape the doc's example shows. This follows the repo's existing
`*TestFixtures.cs` convention (e.g.
`tests/ArchLinterNet.Core.Tests/AttributeUsageContractTestFixtures.cs`),
which already defines marker attributes solely for future scanner discovery
with `#pragma warning disable` for otherwise-unused members.

## Risks / Trade-offs

- [Risk] A reader wants a ready-made attribute package today and finds none.
  → Mitigation: the doc states the recommended path explicitly (copy a
  ~10-line attribute definition into their own codebase) and explains why no
  package exists yet, rather than being silent about it.
- [Risk] A future extraction change (#109+) discovers the compile-only
  fixture's attribute shape doesn't match what the real scanner needs.
  → Mitigation: the fixture mirrors the exact shape already documented and
  reviewed in `docs/policy-format/semantic-classification.md`'s
  `classification.attributes` example; any mismatch is a documentation
  update, not a breaking fixture change, since the fixture asserts nothing
  beyond "this compiles."
- [Trade-off] Choosing "no package" now means adoption requires every project
  to hand-write its own attribute. Accepted: this is the intended default
  per the issue, and a future package remains available as a separate,
  explicitly-decided change if demand emerges.
