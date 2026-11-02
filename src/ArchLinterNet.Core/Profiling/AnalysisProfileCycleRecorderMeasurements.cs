namespace ArchLinterNet.Core.Profiling;

/// <summary>Opt-in workload and cost attribution for cycle baseline-candidate recording.</summary>
public sealed record AnalysisProfileCycleRecorderMeasurements
{
    public int InvocationCount { get; init; }

    public int EnabledInvocationCount { get; init; }

    public int DisabledInvocationCount { get; init; }

    public int EmptyEvidenceInvocationCount { get; init; }

    public int NonEmptyEvidenceInvocationCount { get; init; }

    public long Vertices { get; init; }

    public long Edges { get; init; }

    public long CandidateEvidenceCount { get; init; }

    public long UniqueCandidateEdgePairs { get; init; }

    public long EmittedCandidateCount { get; init; }

    public double ElapsedMs { get; init; }

    public double ComponentIndexBuildElapsedMs { get; init; }

    public double CandidateEvaluationElapsedMs { get; init; }

    public long AllocatedBytes { get; init; }

    public double ObservationOverheadElapsedMs { get; init; }

    public long ObservationOverheadAllocatedBytes { get; init; }

    /// <summary>
    /// One entry per cycle contract that reached the recorder. Entries contain no contract IDs,
    /// paths, graph labels, type names, or diagnostic identities.
    /// </summary>
    public IReadOnlyList<AnalysisProfileCycleRecorderInvocation> Invocations { get; init; } =
        Array.Empty<AnalysisProfileCycleRecorderInvocation>();
}

/// <summary>An anonymized cycle recorder invocation and its joint workload tuple.</summary>
public sealed record AnalysisProfileCycleRecorderInvocation
{
    /// <summary>One of <c>strict</c>, <c>audit</c>, or <c>other</c>.</summary>
    public string Mode { get; init; } = "other";

    public bool RecordingEnabled { get; init; }

    public int Vertices { get; init; }

    public long Edges { get; init; }

    public int CandidateEvidenceCount { get; init; }

    public int UniqueCandidateEdgePairs { get; init; }

    public long EmittedCandidateCount { get; init; }

    public double ElapsedMs { get; init; }

    public double ComponentIndexBuildElapsedMs { get; init; }

    public double CandidateEvaluationElapsedMs { get; init; }

    public long AllocatedBytes { get; init; }

    /// <summary>Wrapper work for collecting graph dimensions and unique evidence pairs.</summary>
    public double ObservationOverheadElapsedMs { get; init; }

    public long ObservationOverheadAllocatedBytes { get; init; }

    public AnalysisProfileCycleRecorderWork Work { get; init; } = new();
}

/// <summary>Deterministic SCC and evidence work performed by one recorder invocation.</summary>
public sealed record AnalysisProfileCycleRecorderWork
{
    public int ComponentIndexBuildCount { get; init; }

    public int PreparedVertices { get; init; }

    public long PreparedEdges { get; init; }

    public int AdjacencyListsBuilt { get; init; }

    public long AdjacencyEdgeVisits { get; init; }

    public long ReverseDegreeEdgeVisits { get; init; }

    public long ReverseFillEdgeVisits { get; init; }

    public int FirstPassVertexVisits { get; init; }

    public long FirstPassEdgeVisits { get; init; }

    public int SecondPassVertexVisits { get; init; }

    public long SecondPassEdgeVisits { get; init; }

    public long CandidateEvidenceChecks { get; init; }

    public long CandidateEdgePresenceChecks { get; init; }

    public long ComponentMembershipChecks { get; init; }

    public long CandidateEmissionCount { get; init; }
}
