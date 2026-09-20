namespace ArchLinterNet.Core.Tests;

internal enum BenchmarkTopologyShape
{
    Linear,
    WideFanOutFanIn,
    Diamond,
    Dense,
    CyclicScc,
    ManyProjectsFewTypes,
    FewProjectsManyTypes,
}

internal enum BenchmarkCompilationMode
{
    RealMsBuild,
    StagedAssemblies,
}

internal enum BenchmarkExecutionMode
{
    SingleCommand,
    FullGovernance,
    MultiCommand,
    PullRequest,
}

internal sealed record BenchmarkDimensionSet
{
    public int ProjectCount { get; init; } = 8;

    public int TypesPerProject { get; init; } = 4;

    public int SourceFilesPerProject { get; init; } = 2;

    public int ReferencesPerProject { get; init; } = 2;

    public int LayerCount { get; init; } = 2;

    public int SelectorMembershipsPerLayer { get; init; } = 2;

    public int ContractsPerRoot { get; init; } = 2;

    public int FindingCandidates { get; init; } = 0;

    public int SourceRootCount { get; init; } = 1;

    public void Validate(BenchmarkTopologyShape shape)
    {
        if (ProjectCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ProjectCount), "A workload must contain at least one project.");
        }

        if (TypesPerProject < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(TypesPerProject));
        }

        if (SourceFilesPerProject < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SourceFilesPerProject));
        }

        if (ReferencesPerProject < 0 || LayerCount < 1 || SelectorMembershipsPerLayer < 1 ||
            ContractsPerRoot < 1 || FindingCandidates < 0 || SourceRootCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(BenchmarkDimensionSet), "Workload dimensions cannot be negative and must contain at least one layer, contract, and source root.");
        }

        int minimumProjects = shape switch
        {
            BenchmarkTopologyShape.WideFanOutFanIn => 3,
            BenchmarkTopologyShape.Diamond => 4,
            _ => 1,
        };
        if (ProjectCount < minimumProjects)
        {
            throw new ArgumentException($"The {shape} shape requires at least {minimumProjects} projects.", nameof(ProjectCount));
        }
    }
}

internal sealed record BenchmarkProjectNode(string Id, string AssemblyName);

internal sealed record BenchmarkProjectEdge(string FromProjectId, string ToProjectId);

internal sealed record BenchmarkTopologySummary
{
    public required int ProjectCount { get; init; }

    public required int ReferenceEdgeCount { get; init; }

    public required int StronglyConnectedComponentCount { get; init; }

    public required bool ContainsCycle { get; init; }

    public required int AlternatePathCount { get; init; }
}

internal sealed record BenchmarkInventoryCounts
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
}

internal sealed record BenchmarkWorkflowDescriptor
{
    public required BenchmarkExecutionMode ExecutionMode { get; init; }

    public required IReadOnlyList<string> CommandFamilies { get; init; }

    public required IReadOnlyList<string> Commands { get; init; }

    public required int IndependentProcesses { get; init; }
}

internal sealed record BenchmarkPullRequestChange
{
    public required IReadOnlyList<string> ChangedProjectIds { get; init; }

    public required IReadOnlyList<string> ChangedSourcePaths { get; init; }

    public required bool IncludesGlobalInput { get; init; }
}

internal sealed record BenchmarkWorkloadDefinition
{
    public const string ManifestSchemaId = "large-solution-benchmark/v1";

    public const string GeneratorVersion = "1";

    public required string WorkloadId { get; init; }

    public required BenchmarkTopologyShape Shape { get; init; }

    public required BenchmarkCompilationMode CompilationMode { get; init; }

    public required BenchmarkDimensionSet Dimensions { get; init; }

    public required IReadOnlyList<BenchmarkProjectNode> Projects { get; init; }

    public required IReadOnlyList<BenchmarkProjectEdge> Edges { get; init; }

    public required BenchmarkTopologySummary Topology { get; init; }

    public required BenchmarkInventoryCounts Inventory { get; init; }

    public required BenchmarkWorkflowDescriptor Workflow { get; init; }

    public required BenchmarkPullRequestChange PullRequestChange { get; init; }

    public required string WorkloadIdentity { get; init; }

    public BenchmarkWorkloadManifest ToManifest() => new()
    {
        SchemaId = ManifestSchemaId,
        GeneratorVersion = GeneratorVersion,
        WorkloadId = WorkloadId,
        Shape = Shape,
        CompilationMode = CompilationMode,
        Dimensions = Dimensions,
        Projects = Projects,
        Edges = Edges,
        Topology = Topology,
        Inventory = Inventory,
        Workflow = Workflow,
        PullRequestChange = PullRequestChange,
        WorkloadIdentity = WorkloadIdentity,
    };
}

internal sealed record BenchmarkWorkloadManifest
{
    public required string SchemaId { get; init; }

    public required string GeneratorVersion { get; init; }

    public required string WorkloadId { get; init; }

    public required BenchmarkTopologyShape Shape { get; init; }

    public required BenchmarkCompilationMode CompilationMode { get; init; }

    public required BenchmarkDimensionSet Dimensions { get; init; }

    public required IReadOnlyList<BenchmarkProjectNode> Projects { get; init; }

    public required IReadOnlyList<BenchmarkProjectEdge> Edges { get; init; }

    public required BenchmarkTopologySummary Topology { get; init; }

    public required BenchmarkInventoryCounts Inventory { get; init; }

    public required BenchmarkWorkflowDescriptor Workflow { get; init; }

    public required BenchmarkPullRequestChange PullRequestChange { get; init; }

    public required string WorkloadIdentity { get; init; }
}
