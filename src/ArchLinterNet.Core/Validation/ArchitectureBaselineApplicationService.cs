using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

public sealed class ArchitectureBaselineApplicationService(
    IArchitectureRunnerSetupService runnerSetupService,
    ArchitectureContractHandlerRegistry handlerRegistry,
    IArchitectureContractExecutor contractExecutor,
    IArchitectureBaselineGenerator baselineGenerator)
    : IArchitectureBaselineApplicationService
{
    public BaselineGenerationOutcome Generate(BaselineGenerationRequest request)
    {
        if (request.Mode is not ("strict" or "audit" or "all"))
        {
            throw new ArgumentException($"Invalid mode: {request.Mode}. Use 'strict', 'audit', or 'all'.", nameof(request));
        }

        ArchitectureContractDocument document = runnerSetupService.LoadDocument(request.PolicyPath);

        ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(
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
            contractExecutor.Execute(runner.Session, "strict", handlerRegistry, includeAsmdefContracts: false);
        }

        if (includeAudit)
        {
            contractExecutor.Execute(runner.Session, "audit", handlerRegistry, includeAsmdefContracts: false);
        }

        ArchitectureBaselineDocument baseline = baselineGenerator.Generate(
            document, runner.BaselineCandidates, request.Reason);

        string yaml = baselineGenerator.Serialize(baseline);

        return new BaselineGenerationOutcome(
            Succeeded: true,
            Yaml: yaml,
            CandidateCount: runner.BaselineCandidates.Count,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>());
    }
}
