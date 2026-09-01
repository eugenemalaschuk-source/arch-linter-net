using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Topology;

/// <summary>Inputs for one read-only topology observation capture.</summary>
public sealed record ArchitectureTopologyCaptureRequest
{
    public required string PolicyPath { get; init; }

    /// <summary>
    /// The supported subject kind to observe: <c>type</c>, <c>namespace</c>, <c>project</c>, or
    /// <c>assembly</c>.
    /// </summary>
    public required string SubjectKind { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyList<string>? PreprocessorSymbols { get; init; }

    public bool IncludeAsmdefContracts { get; init; } = true;

    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }

    public string? RequestedPlatform { get; init; }

    public string? RequestedRuntimeIdentifier { get; init; }

    public int? MaxParallelism { get; init; }

    public CancellationToken CancellationToken { get; init; }
}

/// <summary>One canonical first-party subject observed during topology capture.</summary>
public sealed record ArchitectureTopologyCaptureFact(
    string Identity,
    string SubjectKind,
    string Subject,
    string Project,
    string Assembly);

/// <summary>One canonical directed dependency witness between captured subjects.</summary>
public sealed record ArchitectureTopologyCaptureRelationship(
    string SourceIdentity,
    string TargetIdentity,
    string Witness);

/// <summary>
/// Complete, versioned Core result for one topology capture. The result is review data only: it
/// contains observations and witnesses, never policy declarations or a policy-write operation.
/// </summary>
public sealed record ArchitectureTopologyCaptureOutcome(
    string SubjectKind,
    IReadOnlyList<ArchitectureTopologyCaptureFact> Subjects,
    IReadOnlyList<ArchitectureTopologyCaptureRelationship> Relationships,
    string RepositoryRoot,
    IReadOnlyList<string> PolicyImportPaths,
    IReadOnlyList<string> ResolvedAssemblyPaths,
    IReadOnlyList<string> DiscoveredProjectPaths,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    bool PreflightBlocked = false)
{
    public const int CurrentSchemaVersion = 1;

    public const string DocumentKind = "topology-capture";

    public bool Succeeded => !PreflightBlocked;
}
