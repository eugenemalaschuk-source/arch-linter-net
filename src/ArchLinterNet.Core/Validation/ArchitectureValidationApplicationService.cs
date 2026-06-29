using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public sealed class ArchitectureValidationApplicationService : IArchitectureValidationApplicationService
{
    public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)
    {
        if (request.Mode is not ("strict" or "audit"))
        {
            throw new ArgumentException($"Invalid mode: {request.Mode}. Use 'strict' or 'audit'.", nameof(request));
        }

        using (timing?.Measure("total"))
        {
            ArchitectureContractDocument document;
            string unmatchedConfig;
            string policyConsistencyConfig;
            string coverageConfig;
            ArchitectureRunnerSetup setup;

            using (timing?.Measure("load_and_setup"))
            {
                document = ArchitectureRunnerFactory.LoadDocument(request.PolicyPath, request.BaselinePath, timing);

                unmatchedConfig = document.Analysis.UnmatchedIgnoredViolations;

                if (request.EnforceUnmatchedIgnoredViolationsPolicy && unmatchedConfig is not ("error" or "warn" or "off"))
                {
                    throw new InvalidOperationException(
                        $"Invalid analysis.unmatched_ignored_violations: {unmatchedConfig}. Use 'error', 'warn', or 'off'.");
                }

                policyConsistencyConfig = document.Analysis.PolicyConsistency;

                if (policyConsistencyConfig is not ("error" or "warn" or "off"))
                {
                    throw new InvalidOperationException(
                        $"Invalid analysis.policy_consistency: {policyConsistencyConfig}. Use 'error', 'warn', or 'off'.");
                }

                // Coverage contracts themselves are rejected earlier, in ArchitectureRunnerFactory.LoadDocument
                // (the engine isn't implemented yet; see #97-#103). Validating the severity value here keeps
                // analysis.coverage held to the same "fail fast on malformed config" standard as the other
                // severity settings even though no coverage check currently reads it.
                coverageConfig = document.Analysis.Coverage;

                if (coverageConfig is not ("error" or "warn" or "off"))
                {
                    throw new InvalidOperationException(
                        $"Invalid analysis.coverage: {coverageConfig}. Use 'error', 'warn', or 'off'.");
                }

                HashSet<string>? selectedIds = request.ContractIds is { Count: > 0 }
                    ? new HashSet<string>(request.ContractIds, StringComparer.OrdinalIgnoreCase)
                    : null;

                if (selectedIds != null)
                {
                    HashSet<string> availableIds = CollectAvailableContractIds(document, request.Mode);
                    List<string> unknownIds = selectedIds.Where(id => !availableIds.Contains(id)).ToList();

                    if (unknownIds.Count > 0)
                    {
                        throw new InvalidOperationException(
                            $"Unknown contract IDs: {string.Join(", ", unknownIds)}{Environment.NewLine}" +
                            $"Available IDs in {request.Mode} mode: {string.Join(", ", availableIds.OrderBy(id => id))}");
                    }
                }

                bool enableUnmatchedIgnoreTracking = !request.EnforceUnmatchedIgnoredViolationsPolicy
                    || unmatchedConfig != "off";

                setup = ArchitectureRunnerFactory.BuildRunner(
                    document,
                    request.PolicyPath,
                    request.ConditionSetName,
                    request.PreprocessorSymbols,
                    selectedIds,
                    enableUnmatchedIgnoreTracking,
                    timing,
                    request.Mode);
            }

            ArchitectureContractRunner runner = setup.Runner;
            List<ArchitectureViolation> allViolations = new();

            using (timing?.Measure("configuration_check"))
                allViolations.AddRange(runner.CheckConfiguration(strict: request.Mode == "strict"));

            List<PolicyConsistencyDiagnostic> policyConsistencyFindings;
            using (timing?.Measure("policy_consistency_check"))
            {
                policyConsistencyFindings = policyConsistencyConfig == "off"
                    ? new List<PolicyConsistencyDiagnostic>()
                    : runner.CheckPolicyConsistency();
            }

            ArchitectureContractExecutor.ExecutionResult execution;
            using (timing?.Measure("contract_checks"))
            {
                execution = ArchitectureContractExecutor.Execute(
                    runner, document, request.Mode, request.IncludeAsmdefContracts, timing);
            }

            allViolations.AddRange(execution.Violations);

            IReadOnlyCollection<ArchitectureViolation> coverageFindings = coverageConfig == "off"
                ? Array.Empty<ArchitectureViolation>()
                : execution.CoverageViolations;

            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched =
                Array.Empty<ArchitectureUnmatchedIgnoredViolation>();

            using (timing?.Measure("post_processing"))
            {
                if (request.EnforceUnmatchedIgnoredViolationsPolicy && unmatchedConfig != "off")
                {
                    unmatched = runner.UnmatchedIgnoredViolations;
                }
            }

            if (coverageConfig == "off" && unmatched.Count > 0)
            {
                // When coverage gating is disabled, a stale entry in a strict_coverage/audit_coverage
                // baseline must not surface or block — otherwise turning coverage off would not fully
                // disable the coverage family, only its non-stale findings. Filtered by contract
                // group rather than contract ID: IDs are not guaranteed unique across families (the
                // policy-consistency duplicate-id check does not span coverage contracts), so an
                // id-only filter could mistakenly suppress a stale ignore on an unrelated strict/audit
                // contract that happens to share an ID with a coverage contract.
                unmatched = unmatched
                    .Where(u => u.ContractGroup is not ("strict_coverage" or "audit_coverage"))
                    .ToList();
            }

            bool hasBlockingUnmatched = request.EnforceUnmatchedIgnoredViolationsPolicy
                && unmatchedConfig == "error" && unmatched.Count > 0;

            bool hasBlockingPolicyConsistency =
                policyConsistencyConfig == "error" && policyConsistencyFindings.Count > 0;

            bool hasBlockingCoverage = coverageConfig == "error" && coverageFindings.Count > 0;

            bool passed = allViolations.Count == 0 && execution.Cycles.Count == 0
                && !hasBlockingUnmatched && !hasBlockingPolicyConsistency && !hasBlockingCoverage;

            return new ValidationOutcome(
                passed, allViolations, execution.Cycles, coverageFindings, coverageConfig, unmatched, unmatchedConfig,
                policyConsistencyFindings, policyConsistencyConfig, execution.CoverageSummaries);
        }
    }

    private static HashSet<string> CollectAvailableContractIds(ArchitectureContractDocument document, string mode)
    {
        return ArchitectureContractCatalog.Build(document).AvailableContractIds(mode);
    }
}
