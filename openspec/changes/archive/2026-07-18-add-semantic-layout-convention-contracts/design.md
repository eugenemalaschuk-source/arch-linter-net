## Context

`ArchitectureSourceFileFactIndex` (#171, closed) provides a per-run, lazily-computed index
of declared-type facts enriched with source path data: `SourceFilePath`,
`FileNameWithoutExtension`, `FolderSegments`, `NamespaceSegments`, and Roslyn-accurate
`TypeKind`. It exposes `GetFactsForFile`/`GetFactsForNamespace`/`AllFacts`/`Ambiguities`.

`type_placement_contracts` (existing, closed) is the closest existing pattern: a
`types_matching` selector (`ArchitectureTypeMatcher`) with AND-combined exact/prefix/suffix
string fields, placement/naming expectations, `ignored_violations`, and deterministic
diagnostics — but it selects live `Type` instances via reflection/`TypeIndex`, not files via
source facts. This design follows the same shape (selector → expectations → violations →
diagnostics) applied to `ArchitectureSourceFileFactIndex` instead.

`cel-policy-model` (#162, closed) already normatively fixes a closed `subject` CEL context
shape with 16 members, including `sourcePaths: List[String]` and
`sourceDirectoryPrefixes: List[String]` (repository-relative ancestor directories) — added
specifically so path-aware predicates would not require a future schema change.
`core-cel-integration` (#163, closed) implements the compiler/cache wiring
(`ArchitectureExpressionSchemas`, field-level `CompiledWhen` cache, `ExpressionCompilationValidator`)
and its design.md Decision D4 establishes the "call-site-scoped opt-in" pattern for adding
`when` to a new location without risking existing selector types.

Sixteen files currently implement `type_placement_contracts` end-to-end (family model,
validator, session logic, diagnostic/payload, family registry, family bindings, diagnostic
formatter, SARIF formatter, policy-consistency validator, coverage validator, baseline
loading/comparison/models, validator pipeline registration). The new family follows the
identical fan-out.

## Goals / Non-Goals

**Goals:**
- Add `contracts.strict_layout_conventions` / `contracts.audit_layout_conventions`,
  selecting candidate source files by folder segment, namespace segment, and file-name
  prefix/suffix, validating each matched file's declared types against type-kind, naming,
  file/type-name-correspondence, and matching-interface-counterpart expectations.
- Allow optional CEL `when` refinement per matched declared type, reusing the existing
  `subject` schema and compiled-predicate cache mechanism unchanged.
- Produce deterministic diagnostics that name the matched file/type and, for counterpart
  violations, the expected counterpart name and the requiring source declaration.
- Make missing/unavailable source-path data an explicit, deterministic diagnostic rather
  than a silent no-op, when a layout contract is configured but the index has no
  source-enriched facts at all.
- Remain static-analysis-only: no runtime DI resolution, no regex, no CEL access to
  anything beyond the existing closed `subject` schema.

**Non-Goals:**
- Standalone "unmapped/stale folder" discovery that scans the whole repository for folders
  not covered by any configured layout contract. Audit-mode layout contracts already give
  drift detection for folders authors explicitly configure; open-ended discovery across
  the entire source tree is a distinct, larger feature (its own indexing/reporting surface)
  and is deferred to a follow-up issue rather than bundled into this task.
- Configurable counterpart naming beyond a `name_prefix` (default `"I"`). A full
  pattern-substitution language (`"I{TypeName}"` style templates) is unnecessary for the
  overwhelmingly dominant `I`-prefix .NET convention and would add surface area without a
  concrete second use case.
- Any change to the CEL engine, `ArchLinterNet.CEL`, or the `subject`/`dependency` schema
  shapes themselves. This change consumes #163's existing compiler/schema wiring exactly
  as-is.
- Changing `ArchitectureContextSelector`, `ArchitectureContextSelectorMatcher`, or any
  existing contract family's behavior.

## Decisions

### D1: Candidates are files, not types — a new selector type, not a reused one

`type_placement_contracts` selects `Type` instances directly from `TypeIndex`.
Layout conventions are inherently about *where files live*, so `ArchitectureLayoutFileMatcher`
(`files_matching`) selects **source files** from `ArchitectureSourceFileFactIndex.AllFacts`
grouped by `SourceFilePath`, using `folder_segment` (any element of `FolderSegments` equals
the value), `namespace_segment` (any element of any fact's `NamespaceSegments` in that file
equals the value), `file_name_suffix`/`file_name_prefix` (on `FileNameWithoutExtension`).
All populated fields combine with AND, exact/prefix/suffix comparisons only — matching
`type_placement_contracts`' existing "no regex" precedent.

This is a new type rather than a reuse of `ArchitectureContextSelector` or
`ArchitectureTypeMatcher`: the field vocabulary (folder/namespace/file-name segments) is
structurally different from both, and per D4 below, keeping it a distinct type means the new
`when` location can never leak onto `ArchitecturePortBoundaryContract` or the contextual
dependency/allow-only families the way a shared-type change would risk.

**Alternative considered:** reuse `ArchitectureTypeMatcher` and add folder/namespace-segment
fields to it. Rejected: `ArchitectureTypeMatcher` selects individual types already loaded via
reflection (`layer`, `base_type`, `implements_interface`, `has_attribute` are all
reflection-backed); folding in file-index-backed fields onto the same type would make every
consumer of `ArchitectureTypeMatcher` (including `type_placement_contracts` itself) implicitly
depend on `ArchitectureSourceFileFactIndex` being populated, entangling two independently
optional data sources for no shared benefit.

### D2: `when` compiles against the existing `subject` schema; two new closed locations

Per-file selection narrows a *candidate set of files*; expectations then evaluate against
*declared types within those files*. The natural predicate point is per declared type, so
`files_matching.when` compiles and evaluates against the same `subject` context shape (and
the same `ArchitectureExpressionSchemas.Subject` `CelEnvironment`) that
`layers.<name>.selector.when` already uses — zero new CEL schema surface. `sourcePaths` /
`sourceDirectoryPrefixes` (already in the closed subject shape, added in #162 specifically
for this kind of future path-aware use) let an author write `when: subject.sourceDirectoryPrefixes.exists(p, p.endsWith("Services"))`
or `when: subject.role == 'ApplicationService'` to refine within a matched folder — the
folder/namespace/file-name selector fields remain the coarse, non-CEL filter; `when` is a
bounded, optional refinement on top, matching the issue's constraint that "CEL may refine
selectors only where approved by #162/#164."

`cel-policy-model`'s "First-wave expression locations are closed and selector-scoped"
requirement is amended (delta spec) to add:
- `contracts.strict_layout_conventions[*].files_matching.when`
- `contracts.audit_layout_conventions[*].files_matching.when`

This mirrors exactly how the original seven locations were enumerated — an explicit,
reviewed addition to a closed catalog, not an implicit widening.

**Alternative considered:** no `when` support in this change; defer entirely to a future
task. Rejected: the issue explicitly lists CEL refinement (via #162/#164) as in-scope
("CEL may refine selectors when approved by #162/#164, for example to limit the rule to a
... application-service role"), and since #163/#164 already built every piece needed
(compiler, cache field idiom, `subject` schema with path facts), declining to wire it here
would leave those path facts unused and force a near-identical follow-up change later.

### D3: Load-time validation follows the `TypePlacementValidator` pattern exactly

A new `LayoutConventionsValidator : IArchitecturePolicyDocumentValidator`, registered in
`ArchitecturePolicyDocumentValidatorPipeline`, rejects: a contract with no usable
`files_matching` selector field (same "would match every file" guard
`TypePlacementValidator` uses for types), and a contract with no expectation field
populated (`require_type_kind`/`forbid_type_kind`/naming fields/
`require_type_name_matches_file_name`/`require_matching_interface`). `when` predicates
compile at load time through the existing `ExpressionCompilationValidator` path (extended to
walk `files_matching.when` at the two new locations), fail-closed on any compile diagnostic,
exactly like every other `when` location today.

### D4: Deterministic handling of missing/ambiguous source data

Per file/type-name-based selector fields and expectations inherently require non-null
`SourceFilePath`/`FileNameWithoutExtension`. Facts lacking them (ambiguous partial-class
declarations per #171, or reflection-only facts when `source_roots` is unset) naturally fail
`folder_segment`/`file_name_*` matching and are excluded from the candidate set — this is
deterministic, not silently-wrong, because `namespace_segment`-only selection still works
from reflection-derived namespace facts, and a type simply isn't a candidate for a
file-shaped rule it has no file for.

The one case that would otherwise be silently indistinguishable from "no violations found"
is a contract configured against a run where **no** source enrichment happened at all
(`ArchitectureSourceFileFactIndex.AllFacts` contains zero facts with non-null
`SourceFilePath`) — e.g. `source_roots` misconfigured or omitted. `CheckLayoutConventionsContract`
detects this once per contract and emits a single `LayoutConventionDiagnostic` explaining
that path-based layout checks are unavailable for this run, instead of reporting a clean
zero-violation pass that looks identical to "everything complies."

**Alternative considered:** surface every individual ambiguous/unenriched fact as its own
diagnostic. Rejected: `ArchitectureDeclaredTypeSourceAmbiguity` already exists as a distinct,
already-tested #171 collection for exactly the partial-class case; duplicating that signal
per layout contract would be noisy without adding information — the run-level "no source
facts at all" diagnostic covers the actual gap (an entire contract silently doing nothing),
while scattered per-type ambiguity is already visible via `Ambiguities` for anyone who needs
that detail.

### D5: One configured counterpart check — matching-interface, not a general pairing language

`require_matching_interface: { name_prefix: "I" }` (prefix defaults to `"I"` when the field
is present with no explicit prefix): for every matched concrete class, a fact with
`SimpleTypeName == prefix + ClassSimpleName` and `TypeKind == Interface` must exist
somewhere in `ArchitectureSourceFileFactIndex.AllFacts` (not scoped to the same folder —
interfaces conventionally live in a sibling `Interfaces` folder). This is the one
"paired counterpart check" the issue's acceptance criteria requires ("At least one
configured counterpart check is supported or explicitly deferred with a follow-up issue");
a generalized template/pattern-substitution counterpart language is deferred (see
Non-Goals) since no second concrete counterpart shape is in the issue's scope list.

## Risks / Trade-offs

- **[Risk]** A future contract family reusing `ArchitectureLayoutFileMatcher` might expect
  `when` to be available by default, since it structurally carries the field once added.
  → **Mitigation**: same posture as D4 in `core-cel-integration`'s design — `when`
  acceptance stays call-site-scoped in the raw-YAML key validators, defaulting to rejected;
  this change is the only opt-in call site.
- **[Risk]** `require_matching_interface`'s repo-wide (not folder-scoped) interface lookup
  could match an unrelated same-named interface in a different part of the codebase.
  → **Mitigation**: full `SimpleTypeName` equality (not a substring/fuzzy match) makes an
  accidental collision extremely unlikely in practice, and it mirrors exactly how
  `TryGetFact(fullTypeName)` already treats same-name-different-assembly as ambiguous
  (fail-closed) rather than guessing — a genuine collision surfaces as two candidate
  interfaces, at which point the check reports "no unambiguous matching interface found."
- **[Risk]** Adding two new `when` locations to `cel-policy-model`'s closed list is a spec
  change to an already-shipped, reviewed spec. → **Mitigation**: the spec explicitly
  anticipates this ("adding... any member SHALL require a reviewed change to this
  specification" for the schema; the location list is enumerated precisely so it can be
  extended by a reviewed delta, which is what this change's spec delta is).

## Migration Plan

Additive only. No existing YAML field, contract family, or diagnostic kind changes shape or
behavior. Policies without a `layout_conventions` section are byte-for-byte unaffected —
`ArchitectureSourceFileFactIndex` remains lazy, so a policy that never declares a layout
contract triggers no additional file scanning. No rollback concerns beyond a normal revert.

## Open Questions

None — this change implements against #171/#162/#163, all already closed and reviewed;
the only new normative surface (two `when` locations) is scoped and justified above.
