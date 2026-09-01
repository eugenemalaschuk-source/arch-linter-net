## Context

See [proposal.md](proposal.md) for the motivation. `architecture-health/v1` is the canonical non-compensating gate/health summary, but its current JSON only contains the summary dimensions and reason references. The complete `ArchitectureHealthOutcome` already retains the validation and debt-gate receipts that own policy inventory, lifecycle, applicability, topology, external evidence, finding, and remediation facts. `change report --format json` similarly already supplies a stable delta over complete change snapshots, except that it does not yet retain base-only resolved findings.

The existing `coverage report` command is a separate earlier feature. It reads strict-validation JSON and renders coverage diagnostics for a CI comment; it is neither a source of Architecture Health semantics nor a suitable extension point for this architecture-only PR report.

## Goals / Non-Goals

**Goals:**

- Preserve the existing Health gate and precedence as the sole architecture-acceptance authority.
- Export enough already-evaluated canonical evidence that a report can be generated in a local or CI process without re-analysis or workflow-owned semantics.
- Keep Core responsible for typed artifact validation and semantic report projection, with CLI limited to command orchestration and Markdown presentation.
- Provide deterministic, bounded reviewer drill-down that distinguishes blocking state, known debt, completeness, and change.

**Non-Goals:**

- Change Health, policy-inventory, waiver-lifecycle, applicability, topology, external-evidence, remediation, or policy-weakening semantics.
- Build a generic CI dashboard, query GitHub or vendors, publish a comment, or change workflow permissions (all publication concerns remain #681).
- Infer numeric policy or applicability movement when no canonical base/current evidence supplies it.
- Recompute change results from snapshots while rendering the report.

## Decisions

### 1. Export additive canonical Health report evidence

Health JSON remains `architecture-health/v1` with its existing `schema_id`, `gate`, `health`, and ordered dimensions. Core adds a versioned additive reporting-evidence envelope generated from the same `ArchitectureHealthOutcome` receipts. It contains only the facts a report needs: canonical policy inventory and waiver records, required/evaluable/unassessable applicability evidence, configured topology and external-evidence evidence, normalized current findings and their supplied remediation guidance, debt/policy-weakening receipts, and canonical provenance/navigation identities.

The envelope does not calculate a second health state, read files, parse policy YAML, evaluate dates, or rerun validation. Compatibility-era/missing receipts are represented as missing evidence rather than empty arrays or zero counts. This choice allows the conceptual `report pr --health architecture-health.json --change architecture-change.json` workflow while preserving the existing Health output contract additively.

Alternative considered: require the report command to read multiple strict JSON, inventory, topology, and SARIF artifacts. Rejected because it makes CI callers assemble a semantic data set and invites source-of-truth drift. Alternative: have the report command rerun Health. Rejected because the requested command consumes canonical artifacts and must remain a local deterministic projection.

### 2. Core validates and projects; CLI renders Markdown

Core introduces a versioned report-input reader and a typed `ArchitecturePrReportProjection`. It validates the Health schema and reporting-evidence envelope plus the supplied canonical change document, preserves the reported gate/health rather than resolving it, and creates typed headline, blocker/debt, completeness, change, remediation, and navigation sections. A missing mandatory report receipt produces a report-unavailable projection; it never converts a source `pass` into fabricated usable evidence.

The CLI `report pr` handler performs only path/options/output validation, file I/O, and established exit/error handling. Its renderer translates the Core projection into Markdown with stable sort order and explicit per-section truncation. It does not parse JSON authority fields or decide Health, waiver, topology, applicability, external-evidence, change, or remediation semantics.

Alternative considered: a single Core Markdown formatter. Rejected because Markdown layout is a CLI/report-adapter concern, while the contract requires the Core projection rather than Core ownership of the presentation format.

### 3. Retain resolved findings in the canonical change document

`ArchitectureChangeReports.Compare` will retain findings that occur only in the base snapshot as an ordered `resolved_findings` section in its canonical report. The comparison remains the owner of this fact, so the PR report can disclose added, existing, and resolved findings from its supplied `--change` artifact rather than reopen snapshots or infer resolution from count deltas.

This is additive to the report’s existing sections and preserves the current compatible-mode, condition-set, stable-identity, and complete-snapshot validation contract.

### 4. Bound detail without hiding its scale

The projection keeps full canonical counts and ordered identities. The CLI takes an explicit positive detail limit (defaulted conservatively for PR-comment limits) and limits each detail family independently: blockers/findings, actionable waivers, applicability controls, topology subjects, and external evidence. Every truncated section identifies the omitted count and retains a navigation/artifact identity. Blockers are rendered before non-blocking debt.

### 5. Preserve absence and scope distinctions

`not_configured`, `not_applicable`, and `unassessable` remain distinct report states. The report can show current policy inventory but does not derive a `base → current` rule/waiver/applicability count where the canonical artifact has no paired values; its policy-weakening dimension remains visible instead. Ratios remain literal completeness disclosures, never scores.

## Risks / Trade-offs

- [The Health evidence envelope becomes too broad or duplicates every validation document] → include only report-required canonical facts and reuse existing normalized projections/types where available.
- [Older Health JSON is supplied] → produce an explicit unavailable/evidence-incomplete report shape or fail input validation; never fill defaults.
- [Public Core artifact types grow] → keep the contract versioned, add approved API evidence deliberately, and limit public surface to report artifacts/projection rather than CLI concerns.
- [Large evidence sets exceed comment limits] → deterministic per-family bounds, omitted counts, and canonical navigation identities.
- [A future authority needs new fields] → extend the versioned reporting-evidence payload additively; no renderer-side inference.

## Migration Plan

1. New Health JSON emitted by the current CLI includes reporting evidence; existing summary consumers retain their known top-level fields.
2. `report pr` requires the new evidence envelope and a canonical change report, so legacy artifacts fail closed or render the explicit unavailable shape rather than silently producing a green report.
3. Publication workflow adoption is deferred to #681. Rollback is a normal revert of the report command and additive artifact fields; no persistent data migration occurs.
