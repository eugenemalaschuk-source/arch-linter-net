## Context

The Core already normalizes every reportable diagnostic into an
`ArchitectureFinding`, preserving a canonical `ArchitectureViolationIdentity`,
typed evidence, policy provenance, and a shared JSON projection used directly
by SARIF and the Testing adapter. `DiagnosticDetailProjectionRegistry` keeps
family-specific detail output additive and fails loudly when a diagnostic type
has no registered structured projector.

Issue #120 adds decision-support metadata to that established finding envelope.
It must not change finding identity, evaluate policy a second time, generate an
edit plan, or infer architecture intent from human display text.

## Goals / Non-Goals

**Goals:**

- Provide optional, compact, deterministic remediation guidance where existing
  typed diagnostic and policy evidence supports a bounded repair direction.
- Preserve the same hint semantics in Human, JSON, SARIF, and Testing output.
- Make missing evidence explicit through an optional/no-specialized-hint or
  `review_contract` outcome rather than suggesting an invented seam.
- Keep generation separate from checker/evaluator semantics and protect its
  exact-type registration with a completeness test.

**Non-Goals:**

- Automatic code, YAML, baseline, ignore, or public-API snapshot edits.
- A new policy syntax, evaluator, diagnostic envelope, or SARIF `fix` object.
- LLM-generated recommendations, arbitrary multi-step refactor plans, or
  unbounded user-authored category text.
- Recommending broad ignores, allow-list expansion, exclusions, baselining, or
  strict-to-audit changes as a normal repair.

## Decisions

### One typed optional model on `ArchitectureFinding`

Add an optional `ArchitectureRemediationHint` property to the normalized
finding. The model contains an enum-backed finite category, a deterministic
summary, the stable contract identifier and complete canonical finding identity,
ordered typed evidence entries, optional expected seam/direction, an optional
caveat, and a `RequiresReview` flag. It is additive: a finding without enough
evidence keeps `null`, so existing consumers retain their current behavior.

The category vocabulary is closed and projected as stable snake-case tokens:
`move_code`, `depend_on_abstraction`, `invert_dependency`,
`introduce_adapter`, `use_declared_port`, `fix_classification`,
`fix_policy_input`, `narrow_exception`, `remove_or_replace_dependency`, and
`review_contract`. The factory will only select categories supported by present
facts; retaining a category in the vocabulary is not a license to manufacture
the corresponding architectural seam.

Using the existing `ArchitectureViolationIdentity` inside the hint preserves
assembly-qualified source/target identity for same-named subjects. No display
string is used as an identifier.

### Exact-type remediation-provider registry

Introduce an internal `ArchitectureRemediationHintProviderRegistry`, parallel
to the detail-projection registry. It maps every sealed concrete
`ArchitectureDiagnostic` type to one deterministic provider. Providers can
return no hint deliberately, return a generic `review_contract` hint, or return
a specialized hint. The registry is the only dispatch point; it avoids a new
central diagnostic-type switch and is tested against the complete diagnostic
subtype set.

The factory runs while `ArchitectureFindingMapper` creates each normalized
finding, after canonical identity attribution. It consumes only the typed
diagnostic and identity already produced by Core; checkers, policy loading, and
finding identity attribution remain unchanged.

### Conservative, evidence-specific guidance

Specialized providers use only existing evidence:

- port-boundary diagnostics with a declared expected seam yield
  `use_declared_port` for direct edges and `introduce_adapter` for adapter
  binding failures;
- type-placement and layout evidence yields `move_code`; semantic or location
  mismatches yield `fix_classification` only when the expected/actual facts are
  already present;
- uncovered/stale coverage, preflight, policy consistency, and policy-error
  diagnostics yield `fix_policy_input`;
- external, package, and framework boundaries yield
  `remove_or_replace_dependency` unless a declared adapter/port seam is present
  in their own diagnostic evidence;
- stale ignored-exception diagnostics yield `narrow_exception` with an explicit
  review requirement and a caveat prohibiting wildcard/broad ignore authoring;
- public-surface and unproven dependency/context cases get `review_contract` or
  no hint rather than a guessed abstraction, inversion, ownership boundary, or
  public API snapshot action.

`depend_on_abstraction` and `invert_dependency` remain available only to a
provider that has an already-declared supporting abstraction/direction fact.
They are deliberately not inferred from a forbidden edge alone.

### Shared normalized projection, no second output model

The existing normalized finding JSON projection adds a single `remediation_hint`
object when the property is non-null. Its stable fields include category,
summary, contract identity, canonical finding identity, evidence, expected seam
or direction, caveat, and review flag. Human output appends a concise
`remediation:` clause only when a hint exists. SARIF continues to place the
identical normalized finding below `properties.arch_linter_net`; it adds no
SARIF `fixes` array. The Testing adapter already exposes normalized findings,
so the same property is automatically available to assertions.

This keeps hint projection out of family detail projectors while retaining the
existing registry's structured-output completeness boundary. Tests make a new
diagnostic subtype fail if either its detail projector or remediation provider
is unregistered.

## Risks / Trade-offs

- **[Risk] A hint could imply a policy weakening workaround.** → Providers have
  fixed summaries, no policy-mutating category, and explicit reviewed-exception
  caveats; tests assert prohibited recommendations are absent.
- **[Risk] The public finding envelope changes without bumping its schema.** →
  The field is optional and omitted/null-compatible; the schema version remains
  stable and exact JSON/SARIF parity tests cover both absent and populated
  cases.
- **[Risk] Different outputs drift.** → One formatter projects the normalized
  object and SARIF embeds that exact object; NUnit tests compare Human, JSON,
  SARIF, and Testing semantics.
- **[Trade-off] Some diagnostics receive only `review_contract` or no hint.**
  This is intentional: precision and safety matter more than nominal coverage.

## Migration Plan

No data migration is required. Add the public additive model and mapper factory,
implement registered providers and projection, then add representative family
and parity tests plus public documentation. The reviewed public API snapshot is
updated only through the repository's explicit snapshot workflow. Reverting the
change removes optional guidance without affecting policy evaluation or
canonical finding identity.

## Open Questions

None. Existing typed diagnostic evidence and registry conventions bound the
implementation; unsupported evidence remains a deliberate no-specialized-hint
case.
