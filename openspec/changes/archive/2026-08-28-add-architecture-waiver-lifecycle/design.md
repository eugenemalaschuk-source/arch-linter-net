## Context

Manual `ignored_violations` currently use a legacy glob pair and optional
reason. The runner already records every matched ignore, identifies unmatched
entries, assigns versioned finding identities before suppression, and preserves
the exact composed-policy location. Policy context and the #119 comparer can
already project declared exceptions, but they do not evaluate live findings or
dates. Policy check is intentionally static and must remain assembly-free.

Issue #687 adds accountable waiver debt without replacing baseline debt, scope
exclusions, #119 policy weakening, or #121 gate composition. The affected
surface is Core policy loading/execution/validation, CLI projections, Testing,
policy-context exports, and public migration documentation.

## Goals / Non-Goals

**Goals:**

- Define a backward-compatible structured waiver shape with an exact,
  versioned finding fingerprint and remediation metadata.
- Use a single Core lifecycle evaluator for active, stale, expired,
  metadata-incomplete, and invalid state, retaining composed-policy
  provenance and deterministic ordering.
- Make structured-waiver lifecycle evidence available to validation, JSON,
  human output, Testing, policy context, and existing weakening comparison.
- Keep normal v0.8 strict governance safe: invalid/expired waivers fail closed;
  stale waivers fail policy hygiene; newly added/broadened structured waivers
  are classified by #119 and composed by #121.
- Give v0.7 policies an explicit compatibility path that exposes legacy debt
  without silently breaking their existing matcher behavior.

**Non-Goals:**

- Reimplement baseline lifecycle, #119 weakening detection, or #121 gate
  composition.
- Treat ordinary selectors, scope exclusions, or allow-lists as waivers.
- Query owner/ticket services, use SCM information in identity, auto-create
  tickets, or modify/remove waiver YAML.
- Add repository inventory, Architecture Health, PR report, or badge
  aggregation owned by #685/#679/#680/#682.

## Decisions

### Keep one `ignored_violations` input with an opt-in structured form

A structured entry retains the existing family matcher fields and adds `id`,
`target.fingerprint`, `owner`, `issue`, `introduced`, and `expires`; `reason`
remains required remediation text. The fingerprint is SHA-256 of the existing
versioned `ArchitectureViolationIdentity` canonical representation. This reuses
the live identity assigned before suppression and prevents display text from
becoming a target identity.

The model keeps the legacy glob form exactly as-is. A waiver is structured only
when any structured field is authored; partial structured entries are invalid,
rather than being treated as legacy. Baseline-imported identity entries retain
their existing baseline semantics and are not reclassified as manual waivers.

Alternative considered: a second top-level `waivers` collection. Rejected
because it would duplicate the ownership/matching path and leave existing
suppression semantics split across two models.

### Select strict versus compatibility explicitly by policy schema version

Policy version 1 remains in a `compatibility` waiver profile by default:
legacy entries run and are reported as `metadata_incomplete` debt but do not
change their prior pass/fail behavior. Version 2 selects the `strict` profile
by default; it requires complete structured metadata for manual waivers and
enforces waiver hygiene. An explicit `analysis.waiver_lifecycle_profile`
(`strict` or `compatibility`) overrides the version default only when authored
in policy, and is itself included in effective policy context.

This makes v0.7-to-v0.8 migration intentional, avoids a silent schema break,
and prevents a newly authored v0.8 waiver from becoming anonymous permanent
debt. Compatibility never reports waived architecture as healthy to downstream
consumers: the canonical state remains metadata-incomplete.

Alternative considered: strict profile as a universal default. Rejected because
loading an otherwise valid v0.7 policy would become a surprise blocking change.

### Centralize lifecycle evaluation and use a date-only clock boundary

`ArchitectureWaiverLifecycleEvaluator` receives the policy profile, tracked
matched ignores, validation date, and policy provenance. It returns one record
per configured manual waiver, including state and diagnostics. State precedence
is `invalid`, then `expired`, then `stale`, then `metadata_incomplete`, then
`active`; expired therefore remains blocking even when the underlying finding
is gone. A date is `yyyy-MM-dd`, interpreted without timezone. The validation
request accepts an explicit evaluation date; when omitted, the composed runtime
clock supplies UTC date through one injectable boundary. Every output record
carries the evaluated date so local/CI explanations remain reproducible.

Alternative considered: allow each checker to call local time while matching.
Rejected because it produces timezone-dependent states and scatters lifecycle
semantics among contract families.

### Project evidence; do not re-evaluate it in downstream adapters

Validation outcomes, cache records, formatters, and Testing expose the Core
records directly. CLI human/JSON identify waiver ID, contract/rule, target,
reason, owner, issue, expiry, state, evaluation date, and source location.
Static `policy check` validates schema, duplicate IDs, required structured
metadata, dates, and fingerprint syntax, but does not claim stale/active
runtime state because it performs no architecture analysis.

Policy context adds typed waiver evidence and profile metadata. The #119
exception evaluator compares that evidence: a structured waiver addition or
target broadening creates its normalized change-time finding with existing
severity/configuration; it neither fabricates a persistent baseline entry nor
decides the #121 gate.

## Risks / Trade-offs

- [Fingerprint is difficult to author by hand] → Human/JSON diagnostics expose
  the calculated canonical fingerprint and documentation gives a copyable
  migration workflow.
- [New public Core/Testing records cause API drift] → update approved public
  API snapshots only through the repository's explicit lifecycle and cover
  public API checks.
- [Unmatched tracking is shared across modes and cache paths] → derive records
  once per mode slice and serialize them in the existing cache outcome mapper.
- [Imported fragments can change effective indexes] → resolve provenance from
  the current composed provenance index and base identity on contract/finding
  semantics, never a transient list position.
- [Compatibility could conceal new debt] → structured additions remain #119
  evidence and compatibility records cannot represent healthy waiver state.

## Migration Plan

1. Existing version-1 policies continue to load under compatibility and show
   metadata-incomplete entries when lifecycle enforcement is requested.
2. Teams migrate to version 2 (or explicitly select `strict`), replace manual
   entries with complete structured waivers, and obtain the exact target
   fingerprint from canonical validation output.
3. CI runs strict validation/gate with one explicit evaluation date. Expired,
   invalid, or stale waivers are fixed by changing code or removing/updating the
   reviewed policy; no automatic rewrite occurs.
4. Reverting the feature returns version-1 policy handling without modifying
   user policy files. No data migration or external state is created.

## Open Questions

None. Downstream inventory/Health/report consumers are intentionally deferred
to their owning issues and will consume the public canonical records.
