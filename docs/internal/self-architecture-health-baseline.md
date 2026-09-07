# Self-architecture health baseline and remediation ownership

This is internal project-maintenance evidence for [#804](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/804), the entry task for the [#784](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/784) post-v0.8 self-architecture stabilization story. It records the immutable starting point and the ownership decision for every currently actionable item. It is not a replacement for the canonical CLI JSON authorities.

## Immutable baseline

| Field | Value |
| --- | --- |
| Repository revision | `929983eb985dab934f886cc2f8e25824ac854984` (`main`, 2026-09-07) |
| Producer | [PR #799](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/799), run [34141855753, attempt 1](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/34141855753) |
| Canonical published evidence | [Architecture PR report comment](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/799#issuecomment-5572252282) |
| Policy and selected modes | `architecture/dependencies.arch.yml`; strict gate plus audit evidence |
| Gate / Health | `pass` / `degrading` |
| Effective controls | 81 total: 68 strict, 9 audit, 4 coverage |
| Explicit waiver debt | 14 total: 14 `metadata_incomplete`; 0 active, stale, expired, or invalid |
| Finding debt | 0 reviewed baseline entries; 0 new baseline entries |

The report receipt is the canonical start-state artifact for this exact tree; issue [#784](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/784) records that the receipt matches the merged `main` revision. The audit does not substitute rendered status for the JSON sources: the published report retains Health, inventory, waiver-lifecycle, change, and policy references.

## Health-dimension disposition

`metadata_incomplete` waiver lifecycle is the contributing `degrading` reason. It is evidence of unresolved reviewed structural debt, not proof that PR #799 introduced a regression; the projector regression covers that mapping.

| Evidence | Baseline state | Disposition |
| --- | --- | --- |
| Current strict evaluation | `pass` | Preserve the existing gate; no policy relaxation or baseline expansion. |
| Audit evidence | Non-blocking partial-aggregate inventory | Remediate each exact aggregate below; do not hide it with a broad exclusion. |
| Waiver lifecycle / policy inventory | 14 legacy `metadata_incomplete` entries | Remove an entry only after its type has one handwritten declaration and focused structural proof. Metadata completion alone is not remediation. |
| Applicability | `unavailable` | Missing evidence is not counted as zero or treated as a new structural violation without a concrete product-evidence defect. |
| Topology, metrics, policy weakening, external evidence | `not_configured` | Optional, intentionally unconfigured families are not automatically debt. No adoption task is created merely to raise the rule count. |
| Reviewed and new finding debt | 0 / 0 | No finding-baseline remediation is indicated by this snapshot. |

No incorrect Health/Gate representation or product-evidence defect was established from the canonical record. [#800](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/800) and [#801](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/801) remain the separate signal/presentation owners; they are neither structural-debt remediation nor a prerequisite to recording this baseline.

## Structural-debt ownership matrix

All entries below are exact `production-partial-type-declaration-count-does-not-increase` waivers in `architecture/policy/audit-conventions.arch.yml`. Each remediation owns the listed type and source files, preserves public contracts and canonical analysis authority, introduces purpose-named non-partial collaborators where extraction is necessary, proves compatibility with focused tests, and removes only its own waiver after structural proof.

| Aggregate | Declarations | Owner | Boundary |
| --- | ---: | --- | --- |
| `ArchitectureDiagnosticFormatter` | 15 | [#802](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/802) | Human/JSON/SARIF rendering responsibilities; reuse #777. |
| `ArchitectureSarifFormatter` | 3 | [#802](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/802) | SARIF rendering responsibility; preserve canonical facade. |
| `ValidateCommandHandler` | 5 | [#803](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/803) | Validation orchestration, cache, cancellation, profile, and execution. |
| `ReportCoordinator` | 2 | [#803](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/803) | Report-sink lifecycle versus rendering invocation. |
| `BuildStatePreparationService` | 2 | [#807](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/807) | Ordinary/no-restore/ensure-built state machine versus runtime graph-build construction. |
| `ArchitectureAssemblyResolutionService` | 2 | [#808](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/808) | Artifact resolution versus target-framework path interpretation. |
| `ArchitectureCoverageAnalysisService` | 2 | [#809](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/809) | Coverage analysis versus rule-input coverage projection. |
| `ArchitectureSourceFileFactIndex` | 2 | [#810](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/810) | Source facts versus bounded parallel traversal. |
| `LayoutConventionChecker` | 2 | [#811](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/811) | Convention evaluation versus file-level selector matching. |
| `ArchitectureGraphApplicationService` | 2 | [#812](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/812) | Graph application orchestration versus build-state preflight. |
| `RegularFileHandleReader` | 2 | [#813](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/813) | Native regular-file identity versus repository-local containment traversal. |
| `ArchitecturePublicApiSurfaceScanner` | 2 | [#814](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/814) | Surface materialization versus member enumeration. |
| `ArchitectureBaselineApplicationService` | 2 | [#815](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/815) | Baseline lifecycle versus build-state preparation. |
| `ArchitecturePublicApiApplicationService` | 2 | [#816](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/816) | Public-API operations versus contract/build/surface resolution. |

The security-sensitive negative-regression owners are deliberately distinct: [#807](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/807) retains structured child-process, cancellation, and receipt-trust behavior; [#810](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/810) retains bounded traversal/cancellation/deterministic ordering; and [#813](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/813) retains containment, reparse/symlink, regular-file, and bounded-read defenses.

## Execution graph

```text
freeze #804 baseline at 929983e
        |
        +-- #807 BuildStatePreparationService --+
        |                                       |  independently implementable
        +-- #810 ArchitectureSourceFileFactIndex +
        |
        +-- #802 formatter / SARIF extraction
        +-- #803 validate command / reporting extraction
        +-- #808, #809, #811, #812, #813, #814, #815, #816
        |
        +-- #805 integrated before -> after acceptance
```

The first independently implementable pair is [#807](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/807) and [#810](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/810). Every task has a disjoint production/test boundary; [#813](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/813) remains separate from source-index work because its repository-local I/O threat boundary needs its own focused regressions.

`architecture/policy/audit-conventions.arch.yml` and `openspec/changes/decompose-god-classes/` are shared coordination files. Each remediation PR changes only its own waiver/task hunk, rebases and reconciles serially, and never rewrites either whole file. The strict ratchet remains active throughout; no issue may replace extraction with an exclusion, a broader baseline, a new waiver, or waiver metadata hygiene.

## Verification and closure rule

For each remediation, prove the focused behavior first, run the directly affected suite plus `make fmt`, `make lint-code-size`, `make lint-architecture`, relevant public-API checks, and OpenSpec validation. [#805](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/805) alone integrates the before-to-after baseline and determines whether the parent can claim a clean result. Creating these tasks makes the work owned; it does not make Health healthy or complete [#784](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/784).
