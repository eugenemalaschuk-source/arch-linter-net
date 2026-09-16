# SonarCloud API-risk disposition for issue #888

This report reconciles the exact 47 Core/CLI API-risk findings assigned to [#888](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/888).
The authoritative before evidence is `sonar-debt-baseline-2026-09-13.json`, captured from `main`
revision `103b2680511b702288a95ee4a16189bcec099027` in analysis
`86bce771-6bc9-4856-98b7-2b2f47aa4065`. The issue is a post-v0.8 final Sonar/quality cleanup
slice; it does not change product behavior, policy/schema semantics, public API snapshots,
canonical finding identity/order, cache/build-state, cancellation, or release semantics.

OpenSpec: not applicable. This is a bounded behavior-preserving implementation/evidence change;
the existing `sonarcloud-debt-inventory` capability and archived post-v0.8 debt change already
define the relevant inventory contract. No user-visible capability, schema, or public contract is
introduced.

## Disposition summary

| Result | Count |
| --- | ---: |
| Resolved by behavior-preserving internal concrete-type narrowing | 43 |
| Retained with an individual public/internal-contract rationale | 4 |
| Exact baseline keys covered | 47 |

Expected Sonar result after the PR analysis: 43 baseline keys absent from the analyzed source and
the 4 retained keys still individually explained; no rule suppression, exclusion, baseline refresh,
or quality-gate weakening is used. The after analysis identity and SHA are filled in after the PR's
SonarCloud run.

## Complete key-by-key matrix

| Key | Rule | Frozen baseline location | Disposition | Review rationale |
| --- | --- | --- | --- | --- |
| `AaCaD77vrnQdoRHCV6Iq` | CA1859 | `src/ArchLinterNet.Cli/Commands/Badge/Application/Setup/BadgeSetupCapabilityInspector.cs:368` | resolved | Private JSON array enumerator return type; same candidate order and matching behavior. |
| `AaCaD76xrnQdoRHCV6Im` | CA1859 | `src/ArchLinterNet.Cli/Commands/Badge/Application/Setup/BadgeSetupOutputWriter.cs:229` | resolved | Private generated-file list parameter; caller already owns a `List<T>`. |
| `AaCU5OMByvyiauHFkOXv` | CA1859 | `src/ArchLinterNet.Cli/Commands/Report/Application/PrReportMarkdownRenderer.Health.cs:83` | resolved | Private formatter receives the concrete list produced by the renderer. |
| `AaCU5OQzyvyiauHFkOXw` | CA1859 | `src/ArchLinterNet.Cli/Commands/Report/Application/PrReportMarkdownRenderer.Navigation.cs:66` | resolved | Private bounded-navigation helper uses the concrete string list without changing output order. |
| `AaCBdZFLY_PBEBP_lJB5` | CA1859 | `src/ArchLinterNet.Cli/Commands/Validate/Application/ValidateCommandExecution.cs:300` | retained | The shared internal exit-code helper intentionally accepts the upstream `IReadOnlyList`; narrowing it would require a new materialization/producer contract without a proven benefit. |
| `AaBH7tkI2cmo3hAHmoiQ` | S3218 | `src/ArchLinterNet.Core/Change/ArchitectureChangeSnapshotProjector.cs:119` | resolved | Renamed private record property; projection and semantic values are unchanged. |
| `AaBH7tkI2cmo3hAHmoiP` | S3218 | `src/ArchLinterNet.Core/Change/ArchitectureChangeSnapshotProjector.cs:121` | resolved | Renamed private record property; projection and semantic values are unchanged. |
| `AaBH7tkI2cmo3hAHmoiR` | S3218 | `src/ArchLinterNet.Core/Change/ArchitectureChangeSnapshotProjector.cs:123` | resolved | Renamed private record property; projection and semantic values are unchanged. |
| `AaBZHXF3e2hYynFGUwtR` | CA1859 | `src/ArchLinterNet.Core/Contracts/ArchitectureBaselineLoadingService.cs:187` | resolved | Private validation parameter is always a concrete baseline list. |
| `AaBMI034Z_PDv27CIpM2` | CA1859 | `src/ArchLinterNet.Core/Contracts/Validators/ArchitectureWaiverValidator.cs:45` | resolved | Internal validator receives the concrete dictionary it builds and mutates. |
| `AaBjqBEPK8GPpS9WGlRN` | CA1859 | `src/ArchLinterNet.Core/Contracts/Validators/LayoutConventionApplicabilityValidator.cs:19` | resolved | Private validator receives the concrete inventory list. |
| `AaBjqBEPK8GPpS9WGlRM` | CA1859 | `src/ArchLinterNet.Core/Contracts/Validators/LayoutConventionApplicabilityValidator.cs:41` | resolved | Private validator receives the concrete convention-id hash set. |
| `AaBWsYS7acH2g16CkaeY` | CA1859 | `src/ArchLinterNet.Core/Contracts/Validators/MetricBudgetValidator.cs:22` | resolved | Private validator receives the concrete budget list. |
| `AaBWsYS7acH2g16CkaeZ` | CA1859 | `src/ArchLinterNet.Core/Contracts/Validators/MetricBudgetValidator.cs:23` | resolved | Private validator receives the concrete metric-id hash set. |
| `AaBWsYS7acH2g16CkaeX` | CA1859 | `src/ArchLinterNet.Core/Contracts/Validators/MetricBudgetValidator.cs:24` | resolved | Private validator receives the concrete budget-id hash set. |
| `AaBOp9L3kZtwr73qumRN` | CA1859 | `src/ArchLinterNet.Core/Contracts/Validators/TopologyValidator.cs:13` | resolved | Static internal lookup is constructed as a concrete dictionary and remains read-only by convention. |
| `AaByAPB9Dk5q6SaKHjVl` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureExternalDependencyMetricCalculator.cs:64` | resolved | Private classification lookup returns the concrete array produced by grouping. |
| `AaByAPB9Dk5q6SaKHjVk` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureExternalDependencyMetricCalculator.cs:65` | resolved | Private classification lookup receives the concrete dictionary built by the calculator. |
| `AaBR61XB1fF9NApL5Hqi` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureTopologyEvaluator.cs:247` | resolved | Private relationship builder receives the concrete subject-node dictionary. |
| `AaBWsYIyacH2g16CkaeR` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureTopologyEvaluator.cs:271` | resolved | Private reason builder receives the concrete classification list. |
| `AaBR61XB1fF9NApL5Hqj` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureTopologyEvaluator.cs:272` | resolved | Private reason builder receives the concrete stale-node list. |
| `AaBR61XB1fF9NApL5Hqh` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureTopologyEvaluator.cs:273` | resolved | Private reason builder receives the concrete stale-edge list. |
| `AaBR61XB1fF9NApL5Hqk` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureTopologyEvaluator.cs:307` | resolved | Private violation builder receives the concrete allowed-edge hash set. |
| `AaBxsA2JDk5q6SaKFVHZ` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureTopologyMetricObserver.cs:310` | resolved | Private assembly binding receives the concrete subject dictionary. |
| `AaBxsAv7Dk5q6SaKFVHX` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureTopologyValidationObserver.cs:84` | resolved | Private dependency observer returns the concrete array it materializes. |
| `AaBxsAv7Dk5q6SaKFVHW` | CA1859 | `src/ArchLinterNet.Core/Execution/ArchitectureTopologyValidationObserver.cs:87` | resolved | Private dependency observer receives the concrete type lookup. |
| `AaBjqAmVK8GPpS9WGlRK` | CA1859 | `src/ArchLinterNet.Core/Execution/Checkers/LayoutConventionApplicabilityChecker.cs:174` | resolved | Private subject builder receives the concrete type-identity dictionary. |
| `AaCMbUvU1WTeVOIY0kSJ` | S3871 | `src/ArchLinterNet.Core/History/Enrichment/Exceptions/HistoryDotNetEnrichmentUnavailableException.cs:4` | retained | The exception is an internal enrichment-control-flow type; making it public would expand the reviewed API without a caller requirement. |
| `AaBWsYMeacH2g16CkaeT` | S2365 | `src/ArchLinterNet.Core/Models/ArchitectureMetricMeasurementModels.cs:44` | retained | Public property intentionally returns a defensive collection snapshot; converting it to a method would alter the public contract. |
| `AaBWsYMeacH2g16CkaeU` | S2365 | `src/ArchLinterNet.Core/Models/ArchitectureMetricMeasurementModels.cs:51` | retained | Public property intentionally returns a defensive collection snapshot; converting it to a method would alter the public contract. |
| `AaBUG7g3MZH2Ww3D3jRW` | CA1859 | `src/ArchLinterNet.Core/Models/SarifEvidenceAuthorizationModels.cs:49` | resolved | Private copy helper returns the concrete read-only collection while public properties remain unchanged. |
| `AaBMI1LfZ_PDv27CIpM-` | CA1859 | `src/ArchLinterNet.Core/PolicyContext/ArchitecturePolicyContextApplicationService.cs:508` | resolved | Private waiver projection returns the concrete array it materializes. |
| `AaCU5PYbyvyiauHFkOX2` | CA1859 | `src/ArchLinterNet.Core/Reporting/ArchitecturePrReportProjector.cs:58` | resolved | Private dimension projection returns its concrete array; report order and values are unchanged. |
| `AaBiIir7bkaKLhs5xcKD` | CA1859 | `src/ArchLinterNet.Core/Reporting/ArchitecturePrReportProjector.cs:138` | resolved | Private navigation projection returns its concrete array; report order is unchanged. |
| `AaBiIir7bkaKLhs5xcKB` | CA1859 | `src/ArchLinterNet.Core/Reporting/ArchitecturePrReportProjector.cs:227` | resolved | Private navigation accumulator receives the concrete list it owns. |
| `AaBiIir7bkaKLhs5xcKC` | CA1859 | `src/ArchLinterNet.Core/Reporting/ArchitecturePrReportProjector.cs:245` | resolved | Private navigation accumulator receives the concrete list it owns. |
| `AaBiIiiObkaKLhs5xcJ0` | CA1859 | `src/ArchLinterNet.Core/Reporting/ArchitecturePrReportReader.Receipts.cs:302` | resolved | Private receipt validation receives the concrete arrays from the parsed report. |
| `AaBiIiiObkaKLhs5xcJz` | CA1859 | `src/ArchLinterNet.Core/Reporting/ArchitecturePrReportReader.Receipts.cs:303` | resolved | Private receipt validation receives the concrete arrays from the parsed report. |
| `AaBiIik1bkaKLhs5xcJ7` | CA1859 | `src/ArchLinterNet.Core/Reporting/ArchitecturePrReportReader.cs:227` | resolved | Private availability validation receives the concrete dictionary from deserialization. |
| `AaBOKjsV3f23caWmaEi0` | CA1859 | `src/ArchLinterNet.Core/Validation/ArchitectureApplicabilityEvaluator.cs:65` | resolved | Private assessment builder receives the concrete grouped-record dictionary. |
| `AaBOKjsV3f23caWmaEiz` | CA1859 | `src/ArchLinterNet.Core/Validation/ArchitectureApplicabilityEvaluator.cs:92` | resolved | Private defect collector receives the concrete ordered expected-entry array. |
| `AaBOKjsV3f23caWmaEi1` | CA1859 | `src/ArchLinterNet.Core/Validation/ArchitectureApplicabilityEvaluator.cs:184` | resolved | Private orphan collector receives the concrete expected-entry dictionary. |
| `AaBOKjsV3f23caWmaEi2` | CA1859 | `src/ArchLinterNet.Core/Validation/ArchitectureApplicabilityEvaluator.cs:214` | resolved | Private reason collector returns the concrete array it materializes. |
| `AaCM-eARKm0v-w5CbUC5` | CA1859 | `src/ArchLinterNet.Core/Validation/ArchitectureDebtGateApplicationService.cs:114` | resolved | Private public-API evidence capture returns the concrete list it builds. |
| `AaBZHXUTe2hYynFGUwtc` | CA1859 | `src/ArchLinterNet.Core/Validation/ArchitectureExternalEvidenceApplicabilityProjector.cs:121` | resolved | Private record projection returns the concrete array it materializes; public overloads remain unchanged. |
| `AaBiIi2HbkaKLhs5xcKH` | CA1859 | `src/ArchLinterNet.Core/Validation/ArchitectureHealthApplicationService.cs:68` | resolved | Private evidence attachment receives the concrete outcome array. |
| `AaBiIi0dbkaKLhs5xcKG` | CA1859 | `src/ArchLinterNet.Core/Validation/ArchitectureHealthProjector.ReportEvidence.Debt.cs:124` | resolved | Private lifecycle projection returns the concrete array it materializes. |

## Validation evidence

- `OpenSpec: not applicable` as recorded above; no new or modified capability spec is required.
- `make fmt`, `make lint`, `make lint-architecture`, and `make public-api-check` are required before PR creation.
- Directly affected Core and CLI suites are required before PR creation; full cross-platform acceptance is delegated to PR CI.
- The PR SonarCloud analysis must bind the after result to the final branch SHA and record its analysis identity here before issue closure.
