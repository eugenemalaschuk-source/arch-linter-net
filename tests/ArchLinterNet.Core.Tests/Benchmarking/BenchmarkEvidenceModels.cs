using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

internal enum BenchmarkMeasurementStatus
{
    Available,
    Unavailable,
    NotApplicable,
}

internal enum BenchmarkEvidenceDisposition
{
    Candidate,
    NotReproduced,
    Deferred,
    Implemented,
}

internal sealed record BenchmarkResourceMeasurement
{
    public required BenchmarkMeasurementStatus Status { get; init; }

    public long? Value { get; init; }

    public string? Unit { get; init; }

    public string? Reason { get; init; }

    public static BenchmarkResourceMeasurement Available(long value, string unit) => new()
    {
        Status = BenchmarkMeasurementStatus.Available,
        Value = value,
        Unit = unit,
    };

    public static BenchmarkResourceMeasurement Unavailable(string reason) => new()
    {
        Status = BenchmarkMeasurementStatus.Unavailable,
        Reason = reason,
    };
}

internal sealed record BenchmarkRunDescriptor
{
    public required BenchmarkExecutionMode ExecutionMode { get; init; }

    public required string CacheMode { get; init; }

    public required string PreparedStateMode { get; init; }

    public required string ParallelMode { get; init; }

    public required int SampleOrdinal { get; init; }

    public required bool IsWarmSample { get; init; }
}

internal sealed record BenchmarkDeterministicWorkEvidence
{
    public required int ProjectCount { get; init; }

    public required int AssemblyCount { get; init; }

    public required int SourceFileCount { get; init; }

    public required int TypeCount { get; init; }

    public required int ReferenceEdgeCount { get; init; }

    public required int LayerCount { get; init; }

    public required int SelectorMembershipCount { get; init; }

    public required int ContractCount { get; init; }

    public required int FindingCandidateCount { get; init; }

    public required int SourceRootCount { get; init; }

    public required int GraphTraversalCount { get; init; }

    public required int WitnessPathMaterializationCount { get; init; }

    public required int CanonicalIdentityCandidateCount { get; init; }
}

internal sealed record BenchmarkProfileSample
{
    public required BenchmarkRunDescriptor Run { get; init; }

    public required JsonElement RawAnalysisProfile { get; init; }

    public required string CompletionStatus { get; init; }

    public required int ExitCode { get; init; }

    public required bool OutputFailed { get; init; }

    public required BenchmarkResourceMeasurement WallClock { get; init; }

    public required BenchmarkResourceMeasurement ProcessorTime { get; init; }

    public required BenchmarkResourceMeasurement AllocatedBytes { get; init; }

    public required BenchmarkResourceMeasurement PeakManagedMemory { get; init; }
}

internal sealed record BenchmarkCanonicalResultIdentity
{
    public required string Algorithm { get; init; }

    public required string Sha256 { get; init; }

    public required int FindingCount { get; init; }

    public required string CompletionStatus { get; init; }

    public required int ExitCode { get; init; }
}

internal sealed record BenchmarkFindingIdentity
{
    public required string ContractId { get; init; }

    public required string Kind { get; init; }

    public required string SourceAssembly { get; init; }

    public required string SourceType { get; init; }

    public required string SourceMember { get; init; }

    public required string Location { get; init; }
}

internal sealed record BenchmarkScalePoint
{
    public required string Label { get; init; }

    public required int ScaleValue { get; init; }

    public required int DeterministicWork { get; init; }

    public required int? FindingCount { get; init; }

    public required BenchmarkResourceMeasurement AllocatedBytes { get; init; }
}

internal sealed record BenchmarkComplexityEvidence
{
    public required string ScaleVariable { get; init; }

    public required string CurrentWorkModel { get; init; }

    public required string ObservedGrowth { get; init; }

    public required string TargetWorkModel { get; init; }

    public required BenchmarkEvidenceDisposition Disposition { get; init; }

    public required IReadOnlyList<BenchmarkScalePoint> ScalePoints { get; init; }
}

internal sealed record BenchmarkExpectedEffectEvidence
{
    public required string IssueReference { get; init; }

    public required string TargetPhase { get; init; }

    public required decimal BaselinePhaseShare { get; init; }

    public required string CurrentWorkModel { get; init; }

    public required string TargetWorkModel { get; init; }

    public required decimal ExpectedLocalSpeedupSmall { get; init; }

    public required decimal ExpectedLocalSpeedupMedium { get; init; }

    public required decimal ExpectedLocalSpeedupLarge { get; init; }

    public required decimal ExpectedEndToEndUpperBound { get; init; }

    public required string MemoryAllocationTradeOff { get; init; }

    public required string ColdPathTradeOff { get; init; }

    public required string SuccessThreshold { get; init; }

    public required string KillCriterion { get; init; }

    public required string Confidence { get; init; }
}

internal sealed record BenchmarkEnvironmentEvidence
{
    public required string Runtime { get; init; }

    public required string OperatingSystem { get; init; }

    public required string Architecture { get; init; }

    public required string Configuration { get; init; }

    public required string SourceIdentity { get; init; }

    public required string ToolIdentity { get; init; }

    public required BenchmarkResourceMeasurement PeakWorkingSet { get; init; }

    public required BenchmarkResourceMeasurement ManagedAllocation { get; init; }
}

internal sealed record BenchmarkEvidenceDocument
{
    public const string SchemaId = "benchmark-evidence/v1";

    public required string EvidenceSchemaId { get; init; }

    public required BenchmarkWorkloadManifest Workload { get; init; }

    public required BenchmarkRunDescriptor Run { get; init; }

    public required BenchmarkDeterministicWorkEvidence DeterministicWork { get; init; }

    public required IReadOnlyList<BenchmarkProfileSample> Samples { get; init; }

    public required BenchmarkCanonicalResultIdentity CanonicalResult { get; init; }

    public required BenchmarkEnvironmentEvidence Environment { get; init; }

    public BenchmarkComplexityEvidence? Complexity { get; init; }

    public BenchmarkExpectedEffectEvidence? ExpectedEffect { get; init; }

    public required BenchmarkEvidenceDisposition Disposition { get; init; }

    public void Validate()
    {
        if (!string.Equals(EvidenceSchemaId, SchemaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported benchmark evidence schema '{EvidenceSchemaId}'.");
        }

        if (!string.Equals(Workload.SchemaId, BenchmarkWorkloadDefinition.ManifestSchemaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Benchmark evidence contains an unsupported workload manifest.");
        }

        if (!IsSha256(Workload.WorkloadIdentity) || !IsSha256(CanonicalResult.Sha256))
        {
            throw new InvalidOperationException("Benchmark evidence identities must be lowercase SHA-256 digests.");
        }

        if (Samples.Count == 0)
        {
            throw new InvalidOperationException("Benchmark evidence must retain at least one raw analysis profile.");
        }

        foreach (BenchmarkProfileSample sample in Samples)
        {
            if (!sample.RawAnalysisProfile.TryGetProperty("SchemaId", out JsonElement schemaId) ||
                !string.Equals(schemaId.GetString(), "analysis-profile/v1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Every benchmark sample must retain an analysis-profile/v1 payload.");
            }

            if (sample.OutputFailed && sample.ExitCode == 0)
            {
                throw new InvalidOperationException("A failed output publication cannot be recorded with a successful exit code.");
            }
        }

        if (Complexity is not null && Complexity.Disposition != BenchmarkEvidenceDisposition.NotReproduced &&
            Complexity.ScalePoints.Count < 3)
        {
            throw new InvalidOperationException("A reproduced complexity claim requires at least three scale points.");
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => Uri.IsHexDigit(character)) && value == value.ToLowerInvariant();
}
