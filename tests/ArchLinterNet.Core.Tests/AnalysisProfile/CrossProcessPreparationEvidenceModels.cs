using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Tests;

internal enum PreparationExecutionKind
{
    IndependentProcess,
    InProcessProjection,
    ProcessBoundProjection,
}

internal enum PreparationRevisionRole
{
    Candidate,
    Base,
}

internal enum PreparationBoundaryKind
{
    MsBuildReceipt,
    StagedAssemblies,
}

internal enum PreparationDecisionOutcome
{
    A,
    B,
    C,
}

internal sealed record PreparationProjectionIdentity
{
    public required string CommandFamily { get; init; }

    public required string ProjectionId { get; init; }

    public required string ComparisonGroup { get; init; }

    public required bool ProcessBound { get; init; }
}

internal sealed record PreparationRevisionIdentity
{
    public required PreparationRevisionRole Role { get; init; }

    public required string Identity { get; init; }
}

internal sealed record PreparationProcessIdentity
{
    public required PreparationProjectionIdentity Projection { get; init; }

    public required int ProcessOrdinal { get; init; }

    public required PreparationRevisionRole RevisionRole { get; init; }

    public required PreparationExecutionKind ExecutionKind { get; init; }
}

internal sealed record PreparationResourceEvidence
{
    public required BenchmarkResourceMeasurement StorageBytes { get; init; }

    public required BenchmarkResourceMeasurement IoOperations { get; init; }

    public required BenchmarkResourceMeasurement AllocatedBytes { get; init; }

    public required BenchmarkResourceMeasurement PeakManagedMemory { get; init; }

    public void Validate(string fieldPrefix)
    {
        StorageBytes.Validate($"{fieldPrefix}.storage_bytes");
        IoOperations.Validate($"{fieldPrefix}.io_operations");
        AllocatedBytes.Validate($"{fieldPrefix}.allocated_bytes");
        PeakManagedMemory.Validate($"{fieldPrefix}.peak_managed_memory");
    }
}

internal sealed record CrossProcessPreparationWorkflow
{
    public const string SchemaId = "cross-process-preparation/v1";

    public required string EvidenceSchemaId { get; init; }

    public required string WorkloadIdentity { get; init; }

    public required PreparationBoundaryKind PreparationBoundary { get; init; }

    public required PreparationRevisionIdentity CandidateRevision { get; init; }

    public PreparationRevisionIdentity? BaseRevision { get; init; }

    public required IReadOnlyList<PreparationProjectionIdentity> Projections { get; init; }

    public required IReadOnlyList<string> MeasuredCommandFamilies { get; init; }

    public required IReadOnlyList<string> CacheModesMeasured { get; init; }

    public required bool OneProcessAlternativeMeasured { get; init; }

    public void Validate()
    {
        if (!string.Equals(EvidenceSchemaId, SchemaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported preparation evidence schema '{EvidenceSchemaId}'.");
        }

        if (string.IsNullOrWhiteSpace(WorkloadIdentity))
        {
            throw new InvalidOperationException("Preparation evidence requires a workload identity.");
        }

        ValidateRevision(CandidateRevision, PreparationRevisionRole.Candidate, "candidate_revision");
        if (BaseRevision is not null)
        {
            ValidateRevision(BaseRevision, PreparationRevisionRole.Base, "base_revision");
        }

        if (Projections.Count == 0 || Projections.Select(projection => projection.ProjectionId).Distinct(StringComparer.Ordinal).Count() != Projections.Count)
        {
            throw new InvalidOperationException("Preparation evidence requires unique projections.");
        }

        foreach (PreparationProjectionIdentity projection in Projections)
        {
            if (!PreparationContractIdentity.IsCommandFamily(projection.CommandFamily))
            {
                throw new InvalidOperationException($"Unknown preparation command family '{projection.CommandFamily}'.");
            }

            PreparationContractIdentity.ValidateToken(projection.ProjectionId, "projection_id");
            PreparationContractIdentity.ValidateToken(projection.ComparisonGroup, "comparison_group");
        }

        if (MeasuredCommandFamilies.Any(family =>
                Projections.All(projection => !string.Equals(projection.CommandFamily, family, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException("Every measured command family must have a declared projection.");
        }

        if (MeasuredCommandFamilies.Count == 0 ||
            MeasuredCommandFamilies.Any(family => !PreparationContractIdentity.IsCommandFamily(family)) ||
            MeasuredCommandFamilies.Distinct(StringComparer.Ordinal).Count() != MeasuredCommandFamilies.Count)
        {
            throw new InvalidOperationException("Preparation evidence must declare each measured command family exactly once.");
        }

        if (CacheModesMeasured.Count == 0 || CacheModesMeasured.Any(mode => !PreparationContractIdentity.IsCacheMode(mode)))
        {
            throw new InvalidOperationException("Preparation evidence must declare only known cache modes.");
        }
    }

    private static void ValidateRevision(
        PreparationRevisionIdentity revision,
        PreparationRevisionRole expectedRole,
        string fieldName)
    {
        if (revision.Role != expectedRole)
        {
            throw new InvalidOperationException($"{fieldName} has role '{revision.Role}' but expected '{expectedRole}'.");
        }

        PreparationContractIdentity.ValidateSyntheticIdentity(revision.Identity, fieldName);
    }
}

internal sealed record CrossProcessProcessEvidence
{
    public required PreparationProcessIdentity Identity { get; init; }

    public required BenchmarkProfileSample Sample { get; init; }

    public required BenchmarkCanonicalResultIdentity CanonicalResult { get; init; }

    public required PreparationResourceEvidence Resources { get; init; }

    public void Validate(CrossProcessPreparationWorkflow workflow)
    {
        if (Identity.ProcessOrdinal < 1)
        {
            throw new InvalidOperationException("Preparation process ordinals start at one.");
        }

        if (!workflow.Projections.Contains(Identity.Projection))
        {
            throw new InvalidOperationException("Every process must reference a declared projection.");
        }

        if (Identity.ExecutionKind == PreparationExecutionKind.InProcessProjection && Identity.Projection.ProcessBound)
        {
            throw new InvalidOperationException("A process-bound projection cannot be recorded as in-process.");
        }

        if (Identity.ExecutionKind == PreparationExecutionKind.ProcessBoundProjection && !Identity.Projection.ProcessBound)
        {
            throw new InvalidOperationException("A process-bound projection must declare a process-bound projection identity.");
        }

        if (Identity.RevisionRole == PreparationRevisionRole.Base && workflow.BaseRevision is null)
        {
            throw new InvalidOperationException("Base process evidence requires a declared base revision.");
        }

        if (Sample.Run.SampleOrdinal != Identity.ProcessOrdinal ||
            Sample.CompletionStatus != CanonicalResult.CompletionStatus ||
            Sample.ExitCode != CanonicalResult.ExitCode)
        {
            throw new InvalidOperationException("Process identity, profile sample, and canonical result disagree.");
        }

        Resources.Validate($"processes[{Identity.ProcessOrdinal}].resources");
    }
}

internal sealed record PreparationDecision
{
    public required PreparationDecisionOutcome Outcome { get; init; }

    public required string Route { get; init; }

    public required string Reason { get; init; }

    public required bool OneProcessAlternativeEvaluated { get; init; }

    public required bool BreakEvenObserved { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Route) || string.IsNullOrWhiteSpace(Reason))
        {
            throw new InvalidOperationException("Every #493 decision must include a route and reason.");
        }

        if (Outcome == PreparationDecisionOutcome.A && (!OneProcessAlternativeEvaluated || !BreakEvenObserved))
        {
            throw new InvalidOperationException("Outcome A requires the one-process alternative and break-even effect to be measured.");
        }
    }
}

internal sealed record CrossProcessPreparationEvidenceDocument
{
    public const string SchemaId = "cross-process-preparation-evidence/v1";

    public required string EvidenceSchemaId { get; init; }

    public required BenchmarkEvidenceDocument BenchmarkEvidence { get; init; }

    public required CrossProcessPreparationWorkflow Workflow { get; init; }

    public required IReadOnlyList<CrossProcessProcessEvidence> Processes { get; init; }

    public required PreparedEffectContract PreparedEffect { get; init; }

    public required PreparationDecision Decision { get; init; }

    [JsonIgnore]
    public IReadOnlyList<CrossProcessProcessEvidence> CandidateIndependentProcesses => Processes
        .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
                          process.Identity.ExecutionKind == PreparationExecutionKind.IndependentProcess)
        .ToList();

    public void Validate()
    {
        if (!string.Equals(EvidenceSchemaId, SchemaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported cross-process evidence schema '{EvidenceSchemaId}'.");
        }

        BenchmarkEvidence.Validate();
        Workflow.Validate();
        PreparedEffect.Validate();
        Decision.Validate();

        if (!string.Equals(Workflow.WorkloadIdentity, BenchmarkEvidence.Workload.WorkloadIdentity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cross-process evidence must use the composed benchmark workload identity.");
        }

        if (Processes.Count == 0 || Processes.Select(process => process.Identity.ProcessOrdinal).Distinct().Count() != Processes.Count)
        {
            throw new InvalidOperationException("Cross-process evidence requires unique process ordinals.");
        }

        foreach (CrossProcessProcessEvidence process in Processes)
        {
            process.Validate(Workflow);
        }

        IReadOnlyList<CrossProcessProcessEvidence> candidateProcesses = Processes
            .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate)
            .ToList();

        if (Workflow.OneProcessAlternativeMeasured)
        {
            foreach (string family in Workflow.MeasuredCommandFamilies)
            {
                bool covered = candidateProcesses.Any(process =>
                    string.Equals(process.Identity.Projection.CommandFamily, family, StringComparison.Ordinal) &&
                    process.Identity.ExecutionKind is PreparationExecutionKind.InProcessProjection or
                        PreparationExecutionKind.ProcessBoundProjection);
                if (!covered)
                {
                    throw new InvalidOperationException(
                        $"One-process alternative is incomplete: command family '{family}' has no shared or explicit process-bound projection.");
                }
            }
        }

        if (CandidateIndependentProcesses.Count != PreparedEffect.RepresentativeProcessCount)
        {
            throw new InvalidOperationException("Prepared-effect R must count candidate independent processes only.");
        }

        foreach (IGrouping<string, CrossProcessProcessEvidence> group in candidateProcesses
                     .GroupBy(process => process.Identity.Projection.ComparisonGroup, StringComparer.Ordinal)
                     .Where(group => group.Any(process => process.Identity.ExecutionKind == PreparationExecutionKind.IndependentProcess) &&
                                     group.Any(process => process.Identity.ExecutionKind == PreparationExecutionKind.InProcessProjection)))
        {
            if (group.Select(process => process.CanonicalResult.Sha256).Distinct(StringComparer.Ordinal).Count() != 1)
            {
                throw new InvalidOperationException(
                    $"Equivalent preparation modes in comparison group '{group.Key}' must have equivalent canonical results.");
            }
        }

        if (BenchmarkEvidence.Samples.Count != Processes.Count ||
            BenchmarkEvidence.Samples.Select(sample => sample.Run.SampleOrdinal).OrderBy(ordinal => ordinal)
                .SequenceEqual(Processes.Select(process => process.Identity.ProcessOrdinal).OrderBy(ordinal => ordinal)) is false)
        {
            throw new InvalidOperationException("The composed benchmark envelope must retain every measured process sample.");
        }
    }

}

internal static class PreparationContractIdentity
{
    private static readonly HashSet<string> _commandFamilies = new(StringComparer.Ordinal)
    {
        "strict",
        "audit",
        "no_new_debt",
        "architecture_health",
        "change_snapshot",
        "topology",
        "measure",
        "baseline",
        "public_api",
    };

    private static readonly HashSet<string> _cacheModes = new(StringComparer.Ordinal)
    {
        "disabled",
        "miss",
        "hit",
    };

    public static bool IsCommandFamily(string value) => _commandFamilies.Contains(value);

    public static bool IsCacheMode(string value) => _cacheModes.Contains(value);

    public static void ValidateSyntheticIdentity(string value, string fieldName)
    {
        if (!value.StartsWith("synthetic-", StringComparison.Ordinal) || value.Length == "synthetic-".Length)
        {
            throw new InvalidOperationException($"{fieldName} must be a synthetic identity.");
        }

        ValidateToken(value, fieldName);
    }

    public static void ValidateToken(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace) || value.Contains('/') || value.Contains('\\') ||
            value.Contains("private", StringComparison.OrdinalIgnoreCase) || value.Contains("adopter", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{fieldName} is not a privacy-safe identity.");
        }
    }
}
