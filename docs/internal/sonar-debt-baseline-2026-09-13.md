# SonarCloud debt inventory

- Project: `eugenemalaschuk-source_arch-linter-net`
- Branch: `main`
- Revision: `103b2680511b702288a95ee4a16189bcec099027`
- Analysis date: 2026-09-13T17:14:50+0000
- Quality gate: **ERROR**
- Findings: 460
- Security hotspots: 0

## Quality gate conditions

| Metric | Comparator | Threshold | Actual | Status |
| --- | --- | --- | --- | --- |
| new_coverage | LT | 80 | 88.0 | OK |
| new_duplicated_lines_density | GT | 3 | 0.2 | OK |
| new_maintainability_rating | GT | 1 | 1 | OK |
| new_reliability_rating | GT | 1 | 3 | ERROR |
| new_security_hotspots_reviewed | LT | 100 | 100.0 | OK |
| new_security_rating | GT | 1 | 5 | ERROR |

## Findings by rule

| Rule | Count |
| --- | --- |
| csharpsquid:S1066 | 6 |
| csharpsquid:S107 | 10 |
| csharpsquid:S1144 | 6 |
| csharpsquid:S1172 | 2 |
| csharpsquid:S1192 | 41 |
| csharpsquid:S125 | 3 |
| csharpsquid:S127 | 3 |
| csharpsquid:S1481 | 1 |
| csharpsquid:S1854 | 1 |
| csharpsquid:S2292 | 1 |
| csharpsquid:S2325 | 5 |
| csharpsquid:S2342 | 1 |
| csharpsquid:S2365 | 2 |
| csharpsquid:S2583 | 1 |
| csharpsquid:S2589 | 3 |
| csharpsquid:S2699 | 1 |
| csharpsquid:S3011 | 1 |
| csharpsquid:S3218 | 3 |
| csharpsquid:S3267 | 16 |
| csharpsquid:S3358 | 21 |
| csharpsquid:S3398 | 2 |
| csharpsquid:S3776 | 37 |
| csharpsquid:S3871 | 1 |
| csharpsquid:S3878 | 2 |
| csharpsquid:S3928 | 2 |
| csharpsquid:S4136 | 1 |
| csharpsquid:S8969 | 4 |
| external_roslyn:CA1068 | 3 |
| external_roslyn:CA1822 | 21 |
| external_roslyn:CA1826 | 1 |
| external_roslyn:CA1847 | 1 |
| external_roslyn:CA1859 | 48 |
| external_roslyn:CA1861 | 40 |
| external_roslyn:CA1862 | 1 |
| external_roslyn:CA1865 | 7 |
| external_roslyn:CA2012 | 1 |
| external_roslyn:CA2016 | 1 |
| external_roslyn:CA2101 | 3 |
| external_roslyn:CA2208 | 2 |
| external_roslyn:SYSLIB1054 | 4 |
| python:S1172 | 2 |
| python:S1192 | 11 |
| python:S1481 | 1 |
| python:S1854 | 2 |
| python:S3776 | 12 |
| python:S5443 | 1 |
| python:S5655 | 9 |
| python:S5713 | 6 |
| python:S5778 | 14 |
| python:S5799 | 1 |
| python:S5843 | 1 |
| python:S5886 | 1 |
| python:S6326 | 1 |
| python:S6353 | 3 |
| python:S7500 | 3 |
| python:S7504 | 1 |
| python:S9073 | 2 |
| pythonsecurity:S2083 | 1 |
| pythonsecurity:S8707 | 2 |
| typescript:S1128 | 4 |
| typescript:S1854 | 1 |
| typescript:S2737 | 3 |
| typescript:S3358 | 11 |
| typescript:S3776 | 10 |
| typescript:S5843 | 1 |
| typescript:S5906 | 1 |
| typescript:S6353 | 9 |
| typescript:S6535 | 1 |
| typescript:S6571 | 2 |
| typescript:S6582 | 1 |
| typescript:S6653 | 18 |
| typescript:S7059 | 2 |
| typescript:S7737 | 1 |
| typescript:S7750 | 1 |
| typescript:S7763 | 2 |
| typescript:S7778 | 1 |
| typescript:S7780 | 7 |

## Findings by component

| Component | Count |
| --- | --- |
| eugenemalaschuk-source_arch-linter-net:benchmarks/ArchLinterNet.CEL.Benchmarks/EnvironmentConstructionBenchmarks.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:relay/src/index.ts | 12 |
| eugenemalaschuk-source_arch-linter-net:relay/src/lifecycle.ts | 1 |
| eugenemalaschuk-source_arch-linter-net:relay/src/payload.ts | 18 |
| eugenemalaschuk-source_arch-linter-net:relay/src/read.ts | 1 |
| eugenemalaschuk-source_arch-linter-net:relay/src/registry-do.ts | 4 |
| eugenemalaschuk-source_arch-linter-net:relay/src/relay-do.ts | 35 |
| eugenemalaschuk-source_arch-linter-net:relay/src/security.ts | 3 |
| eugenemalaschuk-source_arch-linter-net:relay/src/types.ts | 1 |
| eugenemalaschuk-source_arch-linter-net:relay/tests/relay.integration.test.ts | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Badge/Application/ArchitectureHealthBadgeDisclosureValidator.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Badge/Application/BadgeLifecycleCommandHandler.cs | 6 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Badge/Application/Setup/BadgeDoctorInspector.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Badge/Application/Setup/BadgeSetupCapabilityInspector.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Badge/Application/Setup/BadgeSetupCommandHandler.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Badge/Application/Setup/BadgeSetupOutputWriter.BundleIntegrity.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Badge/Application/Setup/BadgeSetupOutputWriter.Persistence.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Badge/Application/Setup/BadgeSetupOutputWriter.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Baseline/Application/BaselinePruneCommandHandler.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Measure/Application/MeasureCommandHandler.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Measure/Application/MeasureReportFormatter.cs | 6 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Policy/Application/PolicyWeakeningCommandHandler.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Report/Application/PrReportMarkdownEscaping.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Report/Application/PrReportMarkdownRenderer.Formatting.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Report/Application/PrReportMarkdownRenderer.Health.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Report/Application/PrReportMarkdownRenderer.Navigation.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Report/Application/PrReportMarkdownRenderer.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Report/Application/PrReportNavigationContext.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Topology/Application/TopologyCommandHandler.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Validate/Application/ReportApplicabilityRenderer.cs | 9 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Validate/Application/ReportCoordinator.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Validate/Application/ReportDocumentRenderer.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Validate/Application/StructuredReportRenderer.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Validate/Application/ValidateCommandExecution.cs | 5 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Validate/Application/ValidateCommandHandler.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Commands/Validate/Application/ValidateProfileWriter.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Infrastructure/CliHost.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Cli/Infrastructure/FileIdentityComparer.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/BuildState/BuildStateRuntimeBuildPreparation.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Caching/AnalysisCacheOutcomeMapper.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Caching/AnalysisCacheOutcomeV1.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Change/ArchitectureChangeReport.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Change/ArchitectureChangeSnapshotProjector.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/ArchitectureBaselineComparer.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/ArchitectureBaselineLoadingService.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/RawValidators/RawExternalEvidenceNodeValidator.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/RawValidators/RawTopologyNodeValidator.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/Validators/ArchitectureWaiverValidator.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/Validators/ExternalDiagnosticFilterRules.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/Validators/LayoutConventionApplicabilityValidator.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/Validators/MetricBudgetValidator.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/Validators/MetricDefinitionValidator.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Contracts/Validators/TopologyValidator.cs | 9 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureAnalysisContext.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureContractSurfaceExposureIndex.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureCoverageAnalysisService.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureExternalDependencyMetricCalculator.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureMetricBudgetEvaluator.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureMetricEvaluator.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureTopologyEvaluator.cs | 9 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureTopologyMetricCalculator.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureTopologyMetricObserver.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/ArchitectureTopologyValidationObserver.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/Checkers/CycleChecker.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/Checkers/LayoutConventionApplicabilityChecker.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/SarifEvidenceReader.SourceProjection.cs | 6 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Execution/SarifExternalDiagnosticSelector.cs | 5 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Graph/ArchitectureGraphApplicationService.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/History/Enrichment/Exceptions/HistoryDotNetEnrichmentUnavailableException.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/IO/RepositoryLocalRegularFileReader.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Model/ArchitectureWaiverProfile.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Models/ArchitectureMetricMeasurementModels.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Models/ArchitecturePrReportModels.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Models/SarifEvidenceAuthorizationModels.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Models/SarifEvidenceSourceModels.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/PolicyContext/ArchitecturePolicyContextApplicationService.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/PolicyWeakening/ArchitecturePolicyWeakeningWaiverEvaluator.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Reporting/ArchitectureApplicabilityHumanRenderer.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Reporting/ArchitectureImportedDiagnosticRenderer.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Reporting/ArchitecturePrReportProjector.cs | 7 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Reporting/ArchitecturePrReportReader.Debt.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Reporting/ArchitecturePrReportReader.Receipts.cs | 6 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Reporting/ArchitecturePrReportReader.cs | 7 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Reporting/ArchitectureSarifFormatter.cs | 5 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Scanning/ArchitectureContractSurfaceExposureAttributeScanner.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Scanning/ArchitectureContractSurfaceExposureMemberScanner.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Scanning/ArchitectureContractSurfaceExposureModels.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Scanning/ArchitectureContractSurfaceExposureTraversal.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Scanning/ArchitectureExternalDependencyIlScanner.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Scanning/ArchitecturePublicApiMemberScanner.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureAnalysisSnapshot.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureAnalysisSnapshotEvaluationOrchestrator.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureApplicabilityEvaluator.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureBaselineCandidateCollector.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureDebtGateApplicationService.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureDebtGateFormatter.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureExternalEvidenceApplicabilityProjector.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureHealthApplicationService.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureHealthProjector.ReportEvidence.Debt.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureHealthProjector.ReportEvidence.Receipts.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureHealthProjector.ReportEvidence.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureHealthPublicationEvidenceProjector.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ArchitectureWaiverLifecycleEvaluator.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:src/ArchLinterNet.Core/Validation/ValidationOutcome.cs | 4 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Cli.Tests/CliIntegrationTests.Measure.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Cli.Tests/MeasureReportFormatterTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Cli.Tests/TopologyCommandHandlerTests.cs | 5 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Cli.Tests/ValidateCommandHandlerExternalEvidenceTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureBaselineApplicationServiceBuildStateTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureChangeSnapshotProjectorTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureFindingMapperTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureMetricApplicabilityTests.Completeness.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureMetricApplicabilityTests.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureMetricMeasurementTests.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitecturePolicyContextApplicationServiceTests.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitecturePolicyInventoryProjectorTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitecturePolicyWeakeningComparerTests.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureRunnerSetupServicePreparationTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureTargetFrameworkSelectorTests.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureTopologyEvaluatorTests.cs | 6 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ArchitectureWaiverLifecycleEvaluatorTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/CheckpointBProcessRunner.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/CheckpointBProcessRunnerTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/CheckpointBRestoreReuseTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/CheckpointBV08ValidationPhases.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ContractSurfaceExposureIndexTests.cs | 5 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/ContractSurfaceReferencePolicyTestFixtures.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/CoreTestArchitectureCleanupTests.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/PolicyConsistencyCheckTests.Identity.cs | 1 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/RepeatedWorkRegressionEvidenceTests.cs | 6 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/SarifEvidenceReaderSourceProjectionTests.cs | 7 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/SarifExternalDiagnosticSelectorTests.cs | 3 |
| eugenemalaschuk-source_arch-linter-net:tests/ArchLinterNet.Core.Tests/TopologyPolicyDocumentValidationTests.cs | 2 |
| eugenemalaschuk-source_arch-linter-net:tools/badge_promotion/adapters.py | 6 |
| eugenemalaschuk-source_arch-linter-net:tools/badge_promotion/artifact.py | 3 |
| eugenemalaschuk-source_arch-linter-net:tools/badge_promotion/cli.py | 8 |
| eugenemalaschuk-source_arch-linter-net:tools/badge_promotion/config.py | 2 |
| eugenemalaschuk-source_arch-linter-net:tools/badge_promotion/decision.py | 1 |
| eugenemalaschuk-source_arch-linter-net:tools/badge_promotion/tests/test_badge_promotion.py | 3 |
| eugenemalaschuk-source_arch-linter-net:tools/badge_promotion/tests/test_cli_contracts.py | 9 |
| eugenemalaschuk-source_arch-linter-net:tools/release/main_build.py | 2 |
| eugenemalaschuk-source_arch-linter-net:tools/release/main_quality_coverage.py | 4 |
| eugenemalaschuk-source_arch-linter-net:tools/release/release_distribution.py | 11 |
| eugenemalaschuk-source_arch-linter-net:tools/release/tests/test_create_release_scope_evidence.py | 2 |
| eugenemalaschuk-source_arch-linter-net:tools/release/tests/test_main_build.py | 1 |
| eugenemalaschuk-source_arch-linter-net:tools/release/tests/test_main_build_workflows.py | 2 |
| eugenemalaschuk-source_arch-linter-net:tools/release/tests/test_main_quality_coverage.py | 9 |
| eugenemalaschuk-source_arch-linter-net:tools/release/tests/test_release_distribution.py | 4 |
| eugenemalaschuk-source_arch-linter-net:tools/release/tests/test_release_workflow_package_subjects.py | 1 |
| eugenemalaschuk-source_arch-linter-net:tools/release/verify_relay_dependencies.py | 4 |
| eugenemalaschuk-source_arch-linter-net:tools/release/verify_restored_main_packages.py | 1 |
| eugenemalaschuk-source_arch-linter-net:tools/scripts/test_coverage_badge.py | 1 |

## Disposition summary

| Disposition | Count |
| --- | --- |
| false-positive | 4 |
| untriaged | 456 |
