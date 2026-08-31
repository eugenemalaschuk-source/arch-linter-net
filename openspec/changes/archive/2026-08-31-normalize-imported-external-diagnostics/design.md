## Context

`SarifEvidenceReader` (#520) owns bounded input and current-context trust.  Its valid results
carry immutable authorization, source diagnostics, and artifact provenance.  The #521 selector
turns only those valid inputs into deterministic `SarifSelectedExternalDiagnostic` instances.  No
production consumer currently projects that result into `ArchitectureDiagnostic`,
`ArchitectureFinding`, applicability, or baseline inputs.

The existing normalized finding path is deliberately type-led: a concrete
`ArchitectureDiagnostic` is mapped once into `ArchitectureFinding`, the detail registry owns JSON
fields, `ArchitectureSarifFormatter` embeds the same normalized object, and the Testing adapter
consumes the finding collection.  Applicability is a separate #507 trust/completeness projection;
it must not be replaced by an imported-diagnostic status model.

## Goals / Non-Goals

**Goals:**

- Project #521 selected diagnostics into the existing typed diagnostic/finding path.
- Make persistent identity deterministic and baseline-capable without adding artifact hash, run ID,
  source message, or enumeration order to that identity.
- Preserve original source facts and every selected evidence provenance entry for output-side
  drill-down.
- Map external evidence completion through ordinary applicability expected entries and records.
- Keep strict-selected diagnostics blocking and audit-selected diagnostics reportable but
  non-blocking.

**Non-Goals:**

- Re-read SARIF, invoke analyzers, fetch remote evidence, or revalidate #520 decisions.
- Add a CLI option for artifact paths or a second validation runner.
- Re-emit a nested SARIF log, invent a source analyzer status API, or treat imported data as a
  native architecture fact.
- Change #521 selection/fingerprint semantics or make artifact/run metadata persistent identity.

## Decisions

### 1. Add one diagnostic subtype and project from #521, rather than convert into a native violation

`ImportedExternalDiagnostic` will be a new `ArchitectureDiagnostic` subtype and will retain the
selected diagnostic's source facts, preferred/fallback fingerprint, and ordered
`SarifEvidenceProvenance` collection.  A dedicated projector will accept the #521 selection result
and create this subtype only for selected diagnostics.

This keeps the trust boundary explicit and lets the normal finding mapper, detail registry,
formatters, and Testing adapter operate without source-specific switches.  Reusing a native
`DependencyDiagnostic` was rejected because it would falsely claim that the source result proved a
native architecture dependency.  A separate result envelope was rejected because it would fork
the output/baseline path.

### 2. Treat the #521 canonical selected identity as the stable occurrence reference

The finding identity will be built from the policy-selected canonical identity plus its logical
evidence control and mapped governance mode.  The selected identity already contains the stable
current-context/source dimensions that distinguish locations and required evidence contexts.  The
projector will retain run ID, artifact path, artifact SHA-256, tool version, and source message as
provenance/detail only.

This preserves exact baseline/new-debt behavior across equivalent reruns while still allowing
drill-down to the bytes and run that authorized each occurrence.  Hashing all provenance was
rejected because a new producer run of the same governed occurrence would become artificial debt.

### 3. Model strict/audit at the projection boundary

The selected diagnostic's #521 governance mode controls the projected finding mode and severity.
The projector exposes strict and audit collections through the same collection/result shape used by
other consumers and returns a blocking flag derived only from strict projected findings.  It does
not recompute source severity mapping.

This makes strict/audit semantics deterministic without coupling the reader or selector to
ArchLinterNet's output orchestration.

### 4. Extend existing detail and SARIF projections; never nest the imported document

The detail registry will own a single imported-diagnostic JSON projection with source tool/rule,
severity, message, location/region, source fingerprint origin, and the ordered evidence context
references.  The human formatter presents the concise policy/source identity followed by the same
inspectable provenance.  The SARIF formatter emits one ordinary result at the original source
location with the normalized finding under `properties.arch_linter_net`; it does not copy the
foreign SARIF run/document.

### 5. Use #507 applicability records for evidence status

The projector will derive one external-diagnostic expected control per configured evidence
requirement and one record from the #520/#521 outcome.  Required valid evidence, including a valid
zero-result run, is evaluable; optional deliberate absence is not-applicable; missing,
malformed, filter-mismatched, or wrong-context evidence is unassessable with the existing canonical
reason vocabulary.  It carries the logical evidence control and policy provenance, but never
revalidates reader status.

### 6. Reuse exact baseline candidate identity as an additive projection

The imported-diagnostic projector exposes `ArchitectureBaselineCandidate` values built from the
same structured finding identity.  Baseline-capable consumers can feed them into the existing
generator/comparer; the projection never writes a baseline or derives identity from display text.
Where the current command-level lifecycle cannot yet collect external artifacts, it remains
unchanged; this issue supplies the canonical candidates that #121/#523 consume.

## Risks / Trade-offs

- [Public Core surface expands] → Update approved/public-API snapshots deliberately and assert
  immutability/order in unit tests.
- [A source result has incomplete location data] → Preserve absence as source evidence; never
  synthesize a native source location, and use the #521 canonical identity rather than display
  text for persistence.
- [Output fields drift across sinks] → Centralize the detail projection and assert JSON/SARIF/
  Testing parity from the same selected inputs.
- [Trust gets reinterpreted downstream] → The projector accepts only selected results plus #520
  result statuses; it has no file or producer-service dependency.
- [Baseline group lifecycle is not yet CLI-fed] → Expose exact candidates now and test direct
  baseline generation/comparison, keeping artifact collection and CLI binding outside this issue.

## Migration Plan

The capability is additive. Existing policies without `external_evidence.diagnostic_filter` keep
their trust-only behavior; existing output schema fields remain unchanged while imported findings
add a new discriminated detail kind.  If a consumer has not supplied selected diagnostics, no new
finding or baseline candidate is emitted.  Rollback is removal of the additive projection; no
policy or baseline rewrite is required.
