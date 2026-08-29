## Context

`ArchitectureApplicabilityEvaluator` already validates the expected-membership
and produced-record join, producing `ArchitectureAssessmentCompletionEvidence`.
That evidence retains ordered control assessments and reasons, but the current
CLI adds only a compact completion side channel to JSON/SARIF and the Testing
adapter exposes the raw completion object. It does not enter the normalized
finding stream that Human, JSON, SARIF, Testing, baseline, and debt consumers
already share.

The existing normalized pipeline has two important invariants: typed diagnostic
details must be registered for every concrete diagnostic family, and a finding
identity is an existing `ArchitectureViolationIdentity`, never a formatted
message. The existing evaluator and #685 inventory must remain authorities for
completion and effective-rule counting respectively.

## Goals / Non-Goals

**Goals:**

- Supply one Core-owned projection from completion evidence to typed control
  summaries and normalized unassessable applicability findings.
- Preserve the expected-membership denominator, valid state, integrity defect,
  canonical reason, and provenance unchanged through every adapter.
- Make an applicability regression usable by existing baseline/debt consumers
  through their existing structured identity lifecycle, without allowing known
  debt to turn an unassessable assessment into a trusted pass.
- Keep generic fixtures independent of topology, exposure, budget, and
  imported-diagnostics implementations.

**Non-Goals:**

- New policy fields, YAML parsing, effective-rule inventory, family evaluators,
  health scoring, or a second completeness/debt model.
- Reclassifying valid-but-unassessable evidence as an ordinary conformance
  violation, configuration error, or synthetic clean result.
- Family-specific end-to-end fixtures or output schemas owned by later family
  tasks.

## Decisions

### Project once from the trusted assessment join

A Core projection will consume `ArchitectureAssessmentCompletionEvidence` after
the existing evaluator has completed. It will not receive policy documents,
contract definitions, or raw family evidence. Its summary derives only from
the joined controls: required membership supplies the denominator; a valid
`evaluable` state supplies the numerator; a valid `unassessable` state or any
integrity defect supplies the unassessable count. Optional and not-applicable
membership remain separately counted and visible.

This prevents a renderer from recomputing applicability or accidentally
shrinking the denominator. It also lets #679/#680 consume a stable summary
without coupling this issue to #685 effective-control inventory.

### Emit normalized findings only for insufficiency

Every unassessable record or collection-integrity reason produces an additive
typed applicability diagnostic. Evaluable and deliberately not-applicable
controls are carried by the control projection and summary, not emitted as
failure findings. This keeps successful control evidence inspectable without
turning status rows into artificial violations.

Each finding uses the existing structured identity format, populated from
canonical control identity, family, policy identity, and reason code. A
separate finding is emitted for each distinct semantic insufficiency reason so
existing baseline/debt comparison remains exact; the formatted message is
derived from that identity and may not participate in matching. The projection
deduplicates and orders these records deterministically.

An alternative of emitting one string-only completion message was rejected
because it cannot enter the normalized SARIF/Testing/baseline pipeline. An
alternative of using one global applicability identity was rejected because it
would let unrelated controls suppress one another.

### Keep assessment trust authoritative

Normalized applicability findings expose evidence; they do not recompute or
override completion. Existing strict/audit mode determines their presentation
severity. A matching baseline/debt entry may provide established lifecycle
evidence where that path supports it, but must not make missing required
evidence trusted or convert `unassessable` completion to `pass`.

### Adapt existing outputs rather than add envelopes

The validation outcome and all existing renderers will consume the Core
projection. Human output will add a concise deterministic completeness summary
and unassessable details. JSON and SARIF will carry the same summary and emit
the typed normalized findings through their current finding paths; SARIF keeps
the normalized finding in its established namespaced result property. The
Testing adapter exposes the same findings and summary/completion facts for
assertions. No parallel output or display-text identity is introduced.

### Integrate in two serial slices

The Core projection, typed diagnostic registration, identity formation, and
generic Core tests are one atomic slice. CLI/Testing wiring and output-parity
fixtures depend on it and form a second slice. Running them serially avoids
concurrent edits to shared validation-outcome and normalized-finding paths.

## Risks / Trade-offs

- [A projected record could drift from completion semantics] → derive it only
  from the completed evaluator result and test missing, duplicate, orphan, and
  incompatible-record fixtures.
- [A new diagnostic subtype could be omitted by a formatter] → register it in
  the diagnostic detail registry and cover Human/JSON/SARIF/Testing parity.
- [Baseline treatment could mask assessment insufficiency] → preserve the
  completion state independently and test that a known applicability finding
  does not yield trusted completion.
- [Downstream users may misread a ratio as quality] → label the summary as
  completeness/evaluability and document that it is not a score.

## Migration Plan

The change is additive. Policies with no applicability opt-in continue to
produce no projection. Existing output fields remain intact; consumers receive
new normalized applicability findings and summary data only when canonical
applicability evidence exists. Public API snapshots are updated explicitly
after implementation, with no compatibility migration required.

### Preserve applicability across cached analysis

The analysis-cache wire model must retain expected membership and produced
records. Otherwise a cold run and a cache hit would derive different
completion/projection output for the same effective policy and architecture
evidence. The cache stores canonical inputs and the normal snapshot boundary
continues to derive completion and projection after rehydration; it does not
cache a renderer-specific result.
