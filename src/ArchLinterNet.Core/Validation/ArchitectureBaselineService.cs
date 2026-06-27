using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public static class ArchitectureBaselineService
{
    public static BaselineGenerationOutcome Generate(BaselineGenerationRequest request)
    {
        if (request.Mode is not ("strict" or "audit" or "all"))
        {
            throw new ArgumentException($"Invalid mode: {request.Mode}. Use 'strict', 'audit', or 'all'.", nameof(request));
        }

        ArchitectureContractDocument document = ArchitectureRunnerFactory.LoadDocument(request.PolicyPath);

        ArchitectureRunnerSetup setup = ArchitectureRunnerFactory.BuildRunner(
            document, request.PolicyPath, request.ConditionSetName, enableUnmatchedIgnoreTracking: true);

        ArchitectureContractRunner runner = setup.Runner;

        List<ArchitectureViolation> configViolations = runner.CheckConfiguration(strict: true);
        if (configViolations.Count > 0)
        {
            return new BaselineGenerationOutcome(
                Succeeded: false, Yaml: null, CandidateCount: 0, ConfigurationViolations: configViolations);
        }

        bool includeStrict = request.Mode is "strict" or "all";
        bool includeAudit = request.Mode is "audit" or "all";

        if (includeStrict)
        {
            ArchitectureContractExecutor.Execute(runner, document, "strict", includeAsmdefContracts: false);
        }

        if (includeAudit)
        {
            ArchitectureContractExecutor.Execute(runner, document, "audit", includeAsmdefContracts: false);
        }

        ArchitectureBaselineDocument baseline = ArchitectureBaselineGenerator.Generate(
            document, runner.BaselineCandidates, request.Reason);

        string yaml = ArchitectureBaselineGenerator.Serialize(baseline);

        return new BaselineGenerationOutcome(
            Succeeded: true,
            Yaml: yaml,
            CandidateCount: runner.BaselineCandidates.Count,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>());
    }
}
