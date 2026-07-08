## Context

The CLI's `validate` command supports `--format human` and `--format json` today, both driven by `ArchitectureDiagnosticFormatter` (`src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.cs`, 625 lines — already close to this repo's file-size lint thresholds of warn ≥500 / error ≥800 lines). Diagnostics are represented as a typed `ArchitectureDiagnostic` hierarchy (`src/ArchLinterNet.Core/Model/`), produced from the flat legacy `ArchitectureViolation` record via `ArchitectureDiagnosticMapper`.

Critically, **no diagnostic carries a structured file/line field**. This was an explicit non-goal of the diagnostics-model change (`openspec/changes/archive/2026-06-27-split-diagnostics-model/design.md`). The one exception is method-body diagnostics: `ArchitectureSourceScanner.FindMethodBodyViolations` sets `SourceType` to a repo-relative file path and `ForbiddenNamespace` to the literal `"method-body"`, with each `ForbiddenReferences` entry shaped like `"line {N}: {pattern} -> {symbol}"`. This is enough to reconstruct a `physicalLocation` without any model changes.

`ContractId` is nullable on the model, but in practice `ArchitecturePolicyDocumentLoader.AssignFallbackIds` normalizes every contract's `Name` into an `Id` at policy-load time (via the public `NormalizeToContractId` helper), so it is populated for virtually all real validation runs.

`--mode` (`strict`/`audit`) is a single flag for the whole CLI invocation — contracts are grouped into strict/audit sets at the policy level, not tagged individually on each diagnostic.

## Goals / Non-Goals

**Goals:**
- Add `--format sarif` producing a valid SARIF 2.1.0 document for the violations and cycles already computed by validation.
- Map contract IDs to stable, deduplicated SARIF rule IDs.
- Derive SARIF severity (`level`) from strict/audit mode.
- Populate `physicalLocation` where file/line is available (method-body diagnostics) and `logicalLocations` otherwise.
- Keep output deterministic (stable ordering) so SARIF diffs are meaningful in CI.
- Leave existing human/JSON output byte-identical.

**Non-Goals:**
- Adding structured file/line fields to the diagnostic model itself — this change works within the existing model, using the method-body special case as-is.
- Including coverage findings, unmatched-ignored-violations, or policy-consistency findings in SARIF output. These are policy/meta diagnostics about the linter's own configuration, not violations found in scanned code, and don't fit SARIF's "rule fired against an artifact" shape as cleanly. They remain JSON/human-only.
- Any GitHub Actions workflow changes or automatic code-scanning upload.
- Changing detection/checker logic.

## Decisions

**New formatter class, not an extension of `ArchitectureDiagnosticFormatter`.**
`ArchitectureDiagnosticFormatter.cs` is already near the file-size lint threshold. A new `ArchitectureSarifFormatter` in the same `Reporting` namespace keeps SARIF-specific mapping isolated and avoids tripping `lint-code-size`. This mirrors `ArchitectureGraphFormatter`, which already has sibling `FormatAsJson`/`FormatAsDot`/`FormatAsMermaid` methods on one interface — same shape, new formatter type instead of a fourth method on an already-large class.

**Scope SARIF results to violations + cycles only.**
Alternative considered: mirror the full JSON envelope (violations, cycles, coverage findings, unmatched-ignored-violations, policy-consistency findings). Rejected because SARIF's `result` model represents a rule firing against a location in scanned code; coverage/unmatched-ignore/policy-consistency findings are about the *policy configuration*, not the code, and forcing them into SARIF results would require inventing fake locations or rules that don't correspond to real contract violations. Violations and cycles are exactly the diagnostics that already drive the CLI's exit code and that GitHub code scanning is designed to display.

**Rule ID = `ContractId`, with `NormalizeToContractId` as defensive fallback.**
Since `ContractId` is assigned a normalized fallback at policy-load time, this is almost always already stable. Reusing the same normalization function (rather than inventing a second one) guarantees a diagnostic without a `ContractId` (e.g. constructed directly in a test) still gets the same ID a real policy load would have assigned it.

**Level derived from the single `--mode` flag, not per-diagnostic.**
Alternative considered: track strict/audit per contract on the diagnostic itself. Rejected — the CLI only ever validates one mode's contract set per invocation (`cli-validation` spec: "In strict mode, only strict contracts SHALL be checked"), so every diagnostic in a given SARIF run necessarily belongs to the same mode. Adding a per-diagnostic field would be an unused abstraction.

**Location strategy: physical for method-body, logical for everything else.**
Method-body diagnostics are the only kind with real file/line data (parsed from the existing `"line {N}: ..."` string convention via regex). All other kinds (dependency/layer, external-dependency, package-dependency, type-placement, composition, inheritance, interface-implementation, attribute-usage, public-api-surface, project-metadata, configuration, cycle, assembly-level) use `logicalLocations` with `fullyQualifiedName = SourceType` (or the cycle path) and a small local `kind` hint (`"namespace"`, `"package"`, `"module"`, `"type"`) chosen by a switch on the diagnostic's concrete subtype — not a new lookup table or abstraction, just a direct mapping colocated with the formatter.

If a method-body reference string fails to parse a line number (defensive — the format is internally controlled but not validated against changes elsewhere), the result still includes the file via `artifactLocation.uri` without a `region`, rather than being dropped.

**Deterministic ordering.**
Results are sorted by `(ContractId, SourceType, ForbiddenNamespace)`, matching the sort key already documented for human/JSON output in `openspec/specs/violation-reporting/spec.md`. The `rules` array is deduplicated and sorted alphabetically by `id`.

## Risks / Trade-offs

- [Logical-location `kind` hints are heuristic, not guaranteed accurate for every contract family] → Mitigation: kept as a best-effort hint (SARIF's `logicalLocations[].kind` is an open vocabulary); `fullyQualifiedName` remains the authoritative identifying field regardless of `kind` accuracy.
- [SARIF schema drift if the SARIF spec changes] → Mitigation: pin `version: "2.1.0"` and the corresponding `$schema` URL; this is a static, versioned target.
- [`ArchitectureDiagnosticFormatter.cs` and the new SARIF formatter could diverge in message wording over time] → Mitigation: SARIF result messages reuse the same field values (contract name/id, SourceType, ForbiddenNamespace, references) as the existing human bullet-line convention, just serialized differently.

## Migration Plan

Additive only — new CLI flag value, new formatter class, no changes to existing code paths. No rollback beyond reverting the change; no data migration involved.
