using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

internal static class BenchmarkWorkloadGenerator
{
    public static BenchmarkWorkloadDefinition Create(
        string workloadId,
        BenchmarkTopologyShape shape,
        BenchmarkDimensionSet? dimensions = null,
        BenchmarkCompilationMode compilationMode = BenchmarkCompilationMode.RealMsBuild,
        BenchmarkExecutionMode executionMode = BenchmarkExecutionMode.SingleCommand,
        int independentProcesses = 1)
    {
        ValidateWorkloadId(workloadId);
        BenchmarkDimensionSet resolvedDimensions = dimensions ?? DefaultDimensions(shape);
        resolvedDimensions.Validate(shape);
        long materializedTypeCount = (long)resolvedDimensions.ProjectCount * resolvedDimensions.TypesPerProject;
        if (materializedTypeCount > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimensions),
                "The materialized type count must fit in the deterministic inventory counters.");
        }

        if (resolvedDimensions.FindingCandidates > materializedTypeCount)
        {
            throw new ArgumentException(
                "Finding candidates must target distinct materialized synthetic types.",
                nameof(dimensions));
        }

        if (independentProcesses < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(independentProcesses));
        }

        IReadOnlyList<BenchmarkProjectNode> projects = BenchmarkGraphBuilder.CreateProjects(resolvedDimensions.ProjectCount);
        IReadOnlyList<BenchmarkProjectEdge> edges = BenchmarkGraphBuilder.CreateEdges(
            shape, projects, resolvedDimensions.ReferencesPerProject);
        BenchmarkTopologySummary topology = BenchmarkGraphBuilder.Summarize(projects, edges, shape);
        BenchmarkInventoryCounts inventory = CreateInventory(resolvedDimensions, topology);
        BenchmarkWorkflowDescriptor workflow = CreateWorkflow(executionMode, independentProcesses);
        BenchmarkPullRequestChange change = CreatePullRequestChange(projects, resolvedDimensions);

        var definition = new BenchmarkWorkloadDefinition
        {
            WorkloadId = workloadId,
            Shape = shape,
            CompilationMode = compilationMode,
            Dimensions = resolvedDimensions,
            Projects = projects,
            Edges = edges,
            Topology = topology,
            Inventory = inventory,
            Workflow = workflow,
            PullRequestChange = change,
            WorkloadIdentity = string.Empty,
        };

        string identityInput = BenchmarkJson.Serialize(definition.ToManifest() with { WorkloadIdentity = string.Empty });
        return definition with { WorkloadIdentity = BenchmarkIdentity.Sha256(identityInput) };
    }

    public static BenchmarkWorkloadDefinition CreateVeryLargeSynthetic()
    {
        return Create(
            "synthetic-very-large-multi-project",
            BenchmarkTopologyShape.Dense,
            new BenchmarkDimensionSet
            {
                ProjectCount = 32,
                TypesPerProject = 16,
                SourceFilesPerProject = 8,
                ReferencesPerProject = 31,
                LayerCount = 8,
                SelectorPredicateTermsPerLayer = 16,
                ContractsPerRoot = 8,
                FindingCandidates = 128,
                SourceRootCount = 4,
            },
            BenchmarkCompilationMode.StagedAssemblies,
            BenchmarkExecutionMode.FullGovernance,
            independentProcesses: 6);
    }

    private static BenchmarkInventoryCounts CreateInventory(
        BenchmarkDimensionSet dimensions,
        BenchmarkTopologySummary topology)
    {
        return new BenchmarkInventoryCounts
        {
            ProjectCount = dimensions.ProjectCount,
            AssemblyCount = dimensions.ProjectCount,
            SourceFileCount = dimensions.ProjectCount * dimensions.SourceFilesPerProject * dimensions.SourceRootCount,
            TypeCount = dimensions.ProjectCount * dimensions.TypesPerProject,
            ReferenceEdgeCount = topology.ReferenceEdgeCount,
            LayerCount = dimensions.LayerCount,
            SelectorPredicateEvaluationCount = dimensions.ProjectCount * dimensions.TypesPerProject * dimensions.LayerCount * dimensions.SelectorPredicateTermsPerLayer,
            ContractCount = dimensions.ContractsPerRoot * dimensions.SourceRootCount,
            FindingCandidateCount = dimensions.FindingCandidates,
            SourceRootCount = dimensions.SourceRootCount,
        };
    }

    private static BenchmarkDimensionSet DefaultDimensions(BenchmarkTopologyShape shape) => shape switch
    {
        BenchmarkTopologyShape.ManyProjectsFewTypes => new BenchmarkDimensionSet
        {
            ProjectCount = 32,
            TypesPerProject = 2,
            SourceFilesPerProject = 1,
            ReferencesPerProject = 2,
        },
        BenchmarkTopologyShape.FewProjectsManyTypes => new BenchmarkDimensionSet
        {
            ProjectCount = 4,
            TypesPerProject = 64,
            SourceFilesPerProject = 16,
            ReferencesPerProject = 3,
        },
        _ => new BenchmarkDimensionSet(),
    };

    private static BenchmarkWorkflowDescriptor CreateWorkflow(
        BenchmarkExecutionMode executionMode,
        int independentProcesses)
    {
        IReadOnlyList<string> commandFamilies = executionMode switch
        {
            BenchmarkExecutionMode.SingleCommand => ["validation"],
            BenchmarkExecutionMode.FullGovernance => ["validation", "gate", "health", "change-snapshot", "topology", "metrics"],
            BenchmarkExecutionMode.MultiCommand => ["validation", "gate", "health", "change-snapshot"],
            BenchmarkExecutionMode.PullRequest => ["validation", "gate", "change-snapshot"],
            _ => throw new ArgumentOutOfRangeException(nameof(executionMode), executionMode, "Unknown benchmark execution mode."),
        };

        IReadOnlyList<string> commands = commandFamilies
            .Select(family => family switch
            {
                "validation" => "validation --mode strict",
                "gate" => "gate",
                "health" => "health",
                "change-snapshot" => "change snapshot",
                "topology" => "topology",
                "metrics" => "measure",
                _ => throw new ArgumentOutOfRangeException(nameof(executionMode), executionMode, "Unknown benchmark command family."),
            })
            .ToList();
        return new BenchmarkWorkflowDescriptor
        {
            ExecutionMode = executionMode,
            CommandFamilies = commandFamilies,
            Commands = commands,
            IndependentProcesses = independentProcesses,
        };
    }

    private static BenchmarkPullRequestChange CreatePullRequestChange(
        IReadOnlyList<BenchmarkProjectNode> projects,
        BenchmarkDimensionSet dimensions)
    {
        int changedProjectCount = Math.Min(projects.Count, Math.Max(1, projects.Count / 4));
        string[] changedProjects = projects.Take(changedProjectCount).Select(project => project.Id).ToArray();
        string[] changedSources = changedProjects
            .SelectMany(projectId => Enumerable.Range(1, dimensions.SourceFilesPerProject)
                .Select(index => $"src/{ProjectName(projectId)}/SourceRoot01/Type{index:000}.cs"))
            .ToArray();
        return new BenchmarkPullRequestChange
        {
            ChangedProjectIds = changedProjects,
            ChangedSourcePaths = changedSources,
            IncludesGlobalInput = false,
        };
    }

    private static string ProjectName(string projectId) =>
        projectId.Replace("project-", "Synthetic.Project", StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

    private static void ValidateWorkloadId(string workloadId)
    {
        if (string.IsNullOrWhiteSpace(workloadId) ||
            !workloadId.StartsWith("synthetic-", StringComparison.Ordinal) ||
            workloadId.Length == "synthetic-".Length ||
            workloadId["synthetic-".Length..].Any(character =>
                !((character >= 'a' && character <= 'z') ||
                  (character >= '0' && character <= '9') ||
                  character == '-')))
        {
            throw new ArgumentException(
                "Workload IDs must match the large-solution-workload/v1 pattern 'synthetic-[a-z0-9-]+'.",
                nameof(workloadId));
        }
    }
}

internal static class BenchmarkJson
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, _options);

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options)
        ?? throw new InvalidOperationException("Benchmark JSON deserialized to null.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
