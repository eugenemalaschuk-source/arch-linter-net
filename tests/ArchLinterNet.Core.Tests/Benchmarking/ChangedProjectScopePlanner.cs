namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Taxonomy of PR-shaped changed inputs from issue #503's change-to-project mapping question.
/// Each kind carries one deterministic disposition rule in <see cref="ChangedProjectScopePlanner"/>;
/// no kind is silently dropped from the plan.
/// </summary>
internal enum ChangedInputKind
{
    ProjectOwnedSourceFile,
    SharedOrLinkedSourceFile,
    ProjectReferenceEdgeChange,
    ProjectFilePropertyChange,
    PackageOrFrameworkReferenceChange,
    CentralBuildPropsOrPackagesChange,
    AnalyzerGeneratorAdditionalFileChange,
    PolicyOrImportChange,
    ApiSnapshotOrBaselineChange,
    GeneratedOutputOrBuildContextChange,
}

/// <summary>
/// Disposition a changed input can receive. Matches the design-principle vocabulary in #503:
/// direct mapping, dependency-driven expansion, global expansion, exclusion, or unmappable fallback.
/// </summary>
internal enum ScopeDisposition
{
    DirectMapping,
    DependencyDrivenExpansion,
    GlobalExpansion,
    UnmappableFallback,
}

internal sealed record ChangedInput
{
    public required string InputId { get; init; }

    public required ChangedInputKind Kind { get; init; }

    public required IReadOnlyList<string> OwningProjectIds { get; init; }
}

internal sealed record ChangedInputDecision
{
    public required string InputId { get; init; }

    public required ChangedInputKind Kind { get; init; }

    public required ScopeDisposition Disposition { get; init; }

    public required string Reason { get; init; }

    public required IReadOnlyList<string> DirectProjectIds { get; init; }

    public required IReadOnlyList<string> ExpandedProjectIds { get; init; }
}

internal sealed record ScopePlan
{
    public required IReadOnlyList<ChangedInputDecision> Decisions { get; init; }

    public required IReadOnlyList<string> AffectedProjectIds { get; init; }

    public required int TotalProjectCount { get; init; }

    public int AffectedProjectCount => AffectedProjectIds.Count;

    public decimal AffectedScopeRatio => TotalProjectCount == 0
        ? 0m
        : (decimal)AffectedProjectCount / TotalProjectCount;

    public bool IsFullFallback => AffectedProjectCount == TotalProjectCount &&
        Decisions.Any(decision => decision.Disposition is ScopeDisposition.GlobalExpansion or ScopeDisposition.UnmappableFallback);
}

/// <summary>
/// Pure, deterministic scope-plan calculator for issue #503's evidence gate. It reuses the #502
/// synthetic project graph (<see cref="BenchmarkProjectNode"/>/<see cref="BenchmarkProjectEdge"/>)
/// and answers only "which projects does this changed input affect" — it does not execute analysis,
/// so it carries none of the coverage-accounting or preview/execution-parity guarantees a later
/// production implementation would need. It exists to make the #503 dependency-closure and
/// change-to-project mapping questions measurable rather than assumed.
///
/// Edge direction follows the #502 generator convention: <c>FromProjectId</c> depends on
/// <c>ToProjectId</c> (a forward project reference). The affected set for a changed project is
/// therefore its transitive dependents — every project reachable by walking edges in reverse — since
/// those are the projects whose own validation could be invalidated by the change.
/// </summary>
internal static class ChangedProjectScopePlanner
{
    public static ScopePlan Plan(
        IReadOnlyList<BenchmarkProjectNode> projects,
        IReadOnlyList<BenchmarkProjectEdge> edges,
        IReadOnlyList<ChangedInput> changedInputs)
    {
        if (changedInputs.Count == 0)
        {
            throw new ArgumentException("A scope plan requires at least one changed input.", nameof(changedInputs));
        }

        IReadOnlyList<string> allProjectIds = projects.Select(project => project.Id).ToList();
        Dictionary<string, List<string>> dependentsOf = BuildDependentsIndex(projects, edges);

        var decisions = new List<ChangedInputDecision>();
        var affected = new HashSet<string>(StringComparer.Ordinal);

        foreach (ChangedInput input in changedInputs)
        {
            ChangedInputDecision decision = Decide(input, allProjectIds, dependentsOf);
            decisions.Add(decision);
            affected.UnionWith(decision.ExpandedProjectIds);
        }

        return new ScopePlan
        {
            Decisions = decisions,
            AffectedProjectIds = affected.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            TotalProjectCount = projects.Count,
        };
    }

    private static ChangedInputDecision Decide(
        ChangedInput input,
        IReadOnlyList<string> allProjectIds,
        Dictionary<string, List<string>> dependentsOf)
    {
        switch (input.Kind)
        {
            case ChangedInputKind.ProjectOwnedSourceFile:
            case ChangedInputKind.ProjectFilePropertyChange:
            case ChangedInputKind.PackageOrFrameworkReferenceChange:
                {
                    RequireOwningProjects(input, expectedCount: 1);
                    IReadOnlyList<string> expanded = ClosureOfDependents(input.OwningProjectIds, dependentsOf);
                    return Decision(
                        input,
                        ScopeDisposition.DependencyDrivenExpansion,
                        "Owned by exactly one project; scope widens to that project's transitive dependents because " +
                        "their own validation can observe the change through the reference graph.",
                        expanded);
                }

            case ChangedInputKind.SharedOrLinkedSourceFile:
                {
                    if (input.OwningProjectIds.Count < 2)
                    {
                        throw new ArgumentException(
                            "A shared/linked source file must declare at least two owning projects.",
                            nameof(input));
                    }

                    IReadOnlyList<string> expanded = ClosureOfDependents(input.OwningProjectIds, dependentsOf);
                    return Decision(
                        input,
                        ScopeDisposition.DependencyDrivenExpansion,
                        "Linked into more than one project; scope starts from every owning project and widens to their " +
                        "combined transitive dependents.",
                        expanded);
                }

            case ChangedInputKind.ProjectReferenceEdgeChange:
                {
                    RequireOwningProjects(input, expectedCount: 2);
                    IReadOnlyList<string> expanded = ClosureOfDependents(input.OwningProjectIds, dependentsOf);
                    return Decision(
                        input,
                        ScopeDisposition.DependencyDrivenExpansion,
                        "A project-reference edge changed; both endpoints are seeded because the edge's absence or " +
                        "presence can change graph-shaped contract results (cycles, layering, coverage) for either side.",
                        expanded);
                }

            case ChangedInputKind.CentralBuildPropsOrPackagesChange:
                {
                    return Decision(
                        input,
                        ScopeDisposition.GlobalExpansion,
                        "Directory.Build.*/Directory.Packages.props apply to every project in the solution; no static " +
                        "mapping can bound the affected set below the full project population.",
                        allProjectIds);
                }

            case ChangedInputKind.AnalyzerGeneratorAdditionalFileChange:
                {
                    return Decision(
                        input,
                        ScopeDisposition.GlobalExpansion,
                        "Analyzers/generators/additional files can change compiled output for any consuming project in " +
                        "ways static reference analysis cannot verify; safe widening applies.",
                        allProjectIds);
                }

            case ChangedInputKind.PolicyOrImportChange:
                {
                    return Decision(
                        input,
                        ScopeDisposition.GlobalExpansion,
                        "Policy/import files can change selector membership, layer boundaries, or contract scope for any " +
                        "project; the change is not attributable to one project's dependency subtree.",
                        allProjectIds);
                }

            case ChangedInputKind.ApiSnapshotOrBaselineChange:
                {
                    return Decision(
                        input,
                        ScopeDisposition.UnmappableFallback,
                        "A reviewed public-API-surface contract binds one api_snapshot to an 'assemblies' list that may " +
                        "name more than one project (schema/dependencies.arch.schema.json publicApiSurfaceContract); " +
                        "without a deterministic contract-to-assemblies-to-projects mapping, a single owning project " +
                        "cannot be assumed, so the input is unmappable and falls back to full scope.",
                        allProjectIds);
                }

            case ChangedInputKind.GeneratedOutputOrBuildContextChange:
                {
                    return Decision(
                        input,
                        ScopeDisposition.UnmappableFallback,
                        "Generated output and build-context inputs have no reviewed static ownership mapping; the input " +
                        "is unmappable and falls back to full scope rather than being excluded.",
                        allProjectIds);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(input), input.Kind, "Unknown changed-input kind.");
        }
    }

    private static void RequireOwningProjects(ChangedInput input, int expectedCount)
    {
        if (input.OwningProjectIds.Count != expectedCount)
        {
            throw new ArgumentException(
                $"Changed input '{input.InputId}' of kind {input.Kind} requires exactly {expectedCount} owning project id(s).",
                nameof(input));
        }
    }

    private static ChangedInputDecision Decision(
        ChangedInput input,
        ScopeDisposition disposition,
        string reason,
        IReadOnlyList<string> expanded) => new()
        {
            InputId = input.InputId,
            Kind = input.Kind,
            Disposition = disposition,
            Reason = reason,
            DirectProjectIds = input.OwningProjectIds,
            ExpandedProjectIds = expanded,
        };

    private static Dictionary<string, List<string>> BuildDependentsIndex(
        IReadOnlyList<BenchmarkProjectNode> projects,
        IReadOnlyList<BenchmarkProjectEdge> edges)
    {
        Dictionary<string, List<string>> dependentsOf = projects.ToDictionary(
            project => project.Id,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (BenchmarkProjectEdge edge in edges)
        {
            dependentsOf[edge.ToProjectId].Add(edge.FromProjectId);
        }

        return dependentsOf;
    }

    private static IReadOnlyList<string> ClosureOfDependents(
        IReadOnlyList<string> seeds,
        Dictionary<string, List<string>> dependentsOf)
    {
        var visited = new HashSet<string>(seeds, StringComparer.Ordinal);
        var queue = new Queue<string>(seeds);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (string dependent in dependentsOf[current])
            {
                if (visited.Add(dependent))
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        return visited.OrderBy(id => id, StringComparer.Ordinal).ToList();
    }
}
