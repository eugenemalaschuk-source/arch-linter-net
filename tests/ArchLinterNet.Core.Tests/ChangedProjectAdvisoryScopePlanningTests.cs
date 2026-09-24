using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Evidence tests for issue #503 (changed-project advisory analysis and dependency-aware
/// invalidation). These are pure, deterministic graph computations over the #502 synthetic
/// project-graph generator — no CLI process, MSBuild, or analyzer execution is involved, so unlike
/// the hardware-sensitive benchmark harnesses under AnalysisProfile/, they run in every normal
/// `make test` pass. The K/P values asserted here are the numbers reported in
/// docs/internal/changed-project-advisory-analysis-evidence.md; a change to either file must keep
/// the other honest.
/// </summary>
[TestFixture]
[NonParallelizable]
internal sealed class ChangedProjectAdvisoryScopePlanningTests
{
    private static readonly BenchmarkTopologyShape[] _executableShapes =
    [
        BenchmarkTopologyShape.Linear,
        BenchmarkTopologyShape.WideFanOutFanIn,
        BenchmarkTopologyShape.Diamond,
        BenchmarkTopologyShape.Dense,
    ];

    [TestCaseSource(nameof(_executableShapes))]
    public void LeafProjectChange_AffectsOnlyItself(BenchmarkTopologyShape shape)
    {
        // Measured at all four §3a sizes (8/16/32/64), not just one, so the doc's "K=1 at every
        // scale" row is an actual measurement per size rather than a single P=12 sample generalized
        // by hand.
        foreach (int projectCount in new[] { 8, 16, 32, 64 })
        {
            BenchmarkWorkloadDefinition workload = CreateWorkload(shape, projectCount);
            string leafProjectId = workload.Projects[0].Id;

            ScopePlan plan = ChangedProjectScopePlanner.Plan(
                workload.Projects,
                workload.Edges,
                [OwnedSourceFile("leaf-change", leafProjectId)]);

            Assert.Multiple(() =>
            {
                Assert.That(plan.AffectedProjectIds, Is.EqualTo(new[] { leafProjectId }), $"{shape} P={projectCount}");
                Assert.That(plan.AffectedProjectCount, Is.EqualTo(1), $"{shape} P={projectCount}");
                Assert.That(plan.IsFullFallback, Is.False, $"{shape} P={projectCount}");
            });
        }
    }

    [TestCaseSource(nameof(_executableShapes))]
    public void SharedFoundationProjectChange_AffectsEveryProject(BenchmarkTopologyShape shape)
    {
        // Measured at all four §3a sizes (8/16/32/64), not just one, so the doc's "K=P at every
        // scale" row is an actual measurement per size rather than a single P=12 sample generalized
        // by hand.
        foreach (int projectCount in new[] { 8, 16, 32, 64 })
        {
            BenchmarkWorkloadDefinition workload = CreateWorkload(shape, projectCount);
            string sharedProjectId = workload.Projects[^1].Id;

            ScopePlan plan = ChangedProjectScopePlanner.Plan(
                workload.Projects,
                workload.Edges,
                [OwnedSourceFile("shared-change", sharedProjectId)]);

            Assert.Multiple(() =>
            {
                Assert.That(plan.AffectedProjectCount, Is.EqualTo(projectCount), $"{shape} P={projectCount}");
                Assert.That(plan.AffectedScopeRatio, Is.EqualTo(1.0m), $"{shape} P={projectCount}");
            });
        }
    }

    [Test]
    public void LinearAndDenseMiddleProjectChange_GrowsLinearlyWithPosition()
    {
        foreach (int projectCount in new[] { 8, 16, 32, 64 })
        {
            foreach (BenchmarkTopologyShape shape in new[] { BenchmarkTopologyShape.Linear, BenchmarkTopologyShape.Dense })
            {
                BenchmarkWorkloadDefinition workload = CreateWorkload(shape, projectCount);
                int middleIndex = projectCount / 2;
                string middleProjectId = workload.Projects[middleIndex].Id;

                ScopePlan plan = ChangedProjectScopePlanner.Plan(
                    workload.Projects,
                    workload.Edges,
                    [OwnedSourceFile("middle-change", middleProjectId)]);

                Assert.That(
                    plan.AffectedProjectCount,
                    Is.EqualTo(middleIndex + 1),
                    $"{shape} P={projectCount}: K must equal the changed project's 0-based position + 1.");
            }
        }
    }

    [Test]
    public void WideFanOutFanInSpokeChange_StaysConstantRegardlessOfProjectCount()
    {
        foreach (int projectCount in new[] { 8, 16, 32, 64 })
        {
            BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.WideFanOutFanIn, projectCount);
            string spokeProjectId = workload.Projects[1].Id;

            ScopePlan plan = ChangedProjectScopePlanner.Plan(
                workload.Projects,
                workload.Edges,
                [OwnedSourceFile("spoke-change", spokeProjectId)]);

            Assert.That(
                plan.AffectedProjectCount,
                Is.EqualTo(2),
                $"P={projectCount}: a single fan-out spoke has exactly one dependent (the shared root), independent of scale.");
        }
    }

    [Test]
    public void DiamondTailMiddleProjectChange_GrowsLinearlyPastTheFixedDiamondHead()
    {
        foreach (int projectCount in new[] { 8, 16, 32, 64 })
        {
            BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Diamond, projectCount);
            int middleIndex = projectCount / 2;
            string middleProjectId = workload.Projects[middleIndex].Id;

            ScopePlan plan = ChangedProjectScopePlanner.Plan(
                workload.Projects,
                workload.Edges,
                [OwnedSourceFile("tail-middle-change", middleProjectId)]);

            // Past the fixed 4-node diamond head the tail behaves like a linear chain: the closure is
            // every node from the source through the changed position, i.e. index + 1.
            Assert.That(plan.AffectedProjectCount, Is.EqualTo(middleIndex + 1), $"P={projectCount}");
        }
    }

    [Test]
    public void ProjectReferenceEdgeChange_UnionsBothEndpointDependentClosures()
    {
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Linear, projectCount: 8);
        BenchmarkProjectEdge edge = workload.Edges.Single(e =>
            e.FromProjectId == workload.Projects[3].Id && e.ToProjectId == workload.Projects[4].Id);

        ScopePlan plan = ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [new ChangedInput
            {
                InputId = "edge-change",
                Kind = ChangedInputKind.ProjectReferenceEdgeChange,
                OwningProjectIds = [edge.FromProjectId, edge.ToProjectId],
            }]);

        // The "From" endpoint (index 3) is already a transitive dependent of the "To" endpoint
        // (index 4) via the same edge, so the union collapses to the "To" endpoint's own closure:
        // indices 0..4 (5 projects) plus nothing beyond, since nothing downstream of index 4 changed.
        Assert.That(plan.AffectedProjectCount, Is.EqualTo(5));
    }

    [TestCase(ChangedInputKind.CentralBuildPropsOrPackagesChange, ScopeDisposition.GlobalExpansion)]
    [TestCase(ChangedInputKind.AnalyzerGeneratorAdditionalFileChange, ScopeDisposition.GlobalExpansion)]
    [TestCase(ChangedInputKind.PolicyOrImportChange, ScopeDisposition.GlobalExpansion)]
    [TestCase(ChangedInputKind.ApiSnapshotOrBaselineChange, ScopeDisposition.UnmappableFallback)]
    [TestCase(ChangedInputKind.GeneratedOutputOrBuildContextChange, ScopeDisposition.UnmappableFallback)]
    public void GlobalOrUnmappableInputs_WidenToTheFullProjectPopulation(
        ChangedInputKind kind,
        ScopeDisposition expectedDisposition)
    {
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Dense, projectCount: 16);

        ScopePlan plan = ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [new ChangedInput { InputId = "global-input", Kind = kind, OwningProjectIds = [] }]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.IsFullFallback, Is.True);
            Assert.That(plan.AffectedProjectCount, Is.EqualTo(16));
            Assert.That(plan.Decisions.Single().Disposition, Is.EqualTo(expectedDisposition));
            Assert.That(plan.Decisions.Single().Reason, Is.Not.Empty);
        });
    }

    [Test]
    public void ApiSnapshotChange_DoesNotAssumeExactlyOneOwningProject()
    {
        // A reviewed public-API-surface contract's `assemblies` list can name more than one
        // project (schema/dependencies.arch.schema.json publicApiSurfaceContract), so this kind
        // must accept any owning-project count, including zero or several, without throwing.
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Dense, projectCount: 10);

        ScopePlan plan = ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [new ChangedInput
            {
                InputId = "api-snapshot-change",
                Kind = ChangedInputKind.ApiSnapshotOrBaselineChange,
                OwningProjectIds = [workload.Projects[2].Id, workload.Projects[5].Id],
            }]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Decisions.Single().Disposition, Is.EqualTo(ScopeDisposition.UnmappableFallback));
            Assert.That(plan.AffectedProjectCount, Is.EqualTo(10));
        });
    }

    [Test]
    public void LinkedSourceFileChange_AcceptsASingleOwningProject()
    {
        // MSBuild's <Compile Include="…" Link="…" /> can be used by exactly one project; being
        // "linked" describes how ownership was resolved (evaluated @(Compile) items), not a minimum
        // owner count, so this must behave like ProjectOwnedSourceFile's dependents-closure rule for
        // a single owner rather than rejecting it.
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Linear, projectCount: 8);
        string ownerId = workload.Projects[3].Id;

        ScopePlan plan = ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [new ChangedInput
            {
                InputId = "linked-single-owner",
                Kind = ChangedInputKind.SharedOrLinkedSourceFile,
                OwningProjectIds = [ownerId],
            }]);

        Assert.That(plan.AffectedProjectCount, Is.EqualTo(4), "index-3 owner in an 8-project Linear chain closes to indices 0..3");
    }

    [Test]
    public void SharedSourceFileChange_UnionsMultipleOwningProjectsDependents()
    {
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Linear, projectCount: 8);
        string firstOwnerId = workload.Projects[1].Id;
        string secondOwnerId = workload.Projects[5].Id;

        ScopePlan plan = ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [new ChangedInput
            {
                InputId = "linked-multi-owner",
                Kind = ChangedInputKind.SharedOrLinkedSourceFile,
                OwningProjectIds = [firstOwnerId, secondOwnerId],
            }]);

        // Union of dependents closures for index 1 (K=2: indices 0..1) and index 5 (K=6: indices
        // 0..5) collapses to the deeper owner's own closure, index 5's (K=6).
        Assert.That(plan.AffectedProjectCount, Is.EqualTo(6));
    }

    [Test]
    public void SharedOrLinkedSourceFileChange_RejectsZeroOwningProjects()
    {
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Linear, projectCount: 8);

        Assert.Throws<ArgumentException>(() => ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [new ChangedInput
            {
                InputId = "linked-zero-owner",
                Kind = ChangedInputKind.SharedOrLinkedSourceFile,
                OwningProjectIds = [],
            }]));
    }

    [Test]
    public void EveryChangedInput_ReceivesExactlyOneDeterministicDecision()
    {
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Diamond, projectCount: 10);
        string leaf = workload.Projects[0].Id;
        string shared = workload.Projects[^1].Id;

        ChangedInput[] inputs =
        [
            OwnedSourceFile("a", leaf),
            OwnedSourceFile("b", shared),
            new ChangedInput
            {
                InputId = "c",
                Kind = ChangedInputKind.PackageOrFrameworkReferenceChange,
                OwningProjectIds = [workload.Projects[2].Id],
            },
            new ChangedInput { InputId = "d", Kind = ChangedInputKind.PolicyOrImportChange, OwningProjectIds = [] },
        ];

        ScopePlan plan = ChangedProjectScopePlanner.Plan(workload.Projects, workload.Edges, inputs);

        Assert.That(plan.Decisions, Has.Count.EqualTo(inputs.Length));
        Assert.That(plan.Decisions.Select(decision => decision.InputId), Is.EqualTo(inputs.Select(input => input.InputId)));
    }

    [Test]
    public void RepeatedPlanning_OverSameInputs_IsDeterministic()
    {
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.WideFanOutFanIn, projectCount: 20);
        ChangedInput[] inputs = [OwnedSourceFile("x", workload.Projects[5].Id)];

        ScopePlan first = ChangedProjectScopePlanner.Plan(workload.Projects, workload.Edges, inputs);
        ScopePlan second = ChangedProjectScopePlanner.Plan(workload.Projects, workload.Edges, inputs);

        Assert.That(first.AffectedProjectIds, Is.EqualTo(second.AffectedProjectIds));
    }

    [Test]
    public void EvaluatorFamilies_RequireDifferentScopesThanTheGenericDependentsClosure()
    {
        // Middle-position change in a 16-project Linear chain: the generic reference-graph
        // dependents closure (ChangedProjectScopePlanner's default) is K=9 (see
        // LinearAndDenseMiddleProjectChange_GrowsLinearlyWithPosition). No single number is
        // correct for every evaluator family — this is #503's own requirement, not an assumption.
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Linear, projectCount: 16);
        string changedProjectId = workload.Projects[8].Id;
        IReadOnlyList<string> allProjectIds = workload.Projects.Select(project => project.Id).ToList();
        IReadOnlyList<string> changedProjectIds = [changedProjectId];
        List<string> dependentsClosureIds = ChangedProjectScopePlanner.DependentsClosure(
            workload.Projects, workload.Edges, changedProjectIds);

        ScopePlan genericPlan = ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [OwnedSourceFile("middle-change", changedProjectId)]);
        Assert.That(genericPlan.AffectedProjectCount, Is.EqualTo(9), "generic dependents-closure baseline");
        Assert.That(dependentsClosureIds, Has.Count.EqualTo(9), "DependentsClosure must agree with Plan's own closure");

        EvaluatorScope referenceGraphLocal = EvaluatorFamilyScopePlanner.Plan(
            EvaluatorFamily.ReferenceGraphLocal, changedProjectIds, dependentsClosureIds, allProjectIds);
        EvaluatorScope cyclesGlobal = EvaluatorFamilyScopePlanner.Plan(
            EvaluatorFamily.CyclesGlobal, changedProjectIds, dependentsClosureIds, allProjectIds);
        EvaluatorScope contractCoListing = EvaluatorFamilyScopePlanner.Plan(
            EvaluatorFamily.ContractCoListing, changedProjectIds, dependentsClosureIds, allProjectIds);
        EvaluatorScope aggregatedGlobalScan = EvaluatorFamilyScopePlanner.Plan(
            EvaluatorFamily.AggregatedGlobalScan, changedProjectIds, dependentsClosureIds, allProjectIds);
        EvaluatorScope coverageGraphOrCatalogWide = EvaluatorFamilyScopePlanner.Plan(
            EvaluatorFamily.CoverageGraphOrCatalogWide, changedProjectIds, dependentsClosureIds, allProjectIds);

        Assert.Multiple(() =>
        {
            // Layers/external/allow_only checkers scan only the changed project's own outgoing
            // references, but the verdict depends on each referenced TARGET type's own
            // classification (namespace/role/expression facts) — a change to the target project can
            // flip an unchanged dependent's result. The safe bound is therefore the full dependents
            // closure (9), the same as the generic default, not the changed project alone.
            Assert.That(referenceGraphLocal.RequiredProjectIds, Is.EqualTo(dependentsClosureIds));

            // Cycle detection shares one graph across every layer in the contract: even the
            // dependents closure is not a safe bound, so this evidence task falls back to the full
            // population (16), which is wider than the generic closure (9), not narrower.
            Assert.That(cyclesGlobal.RequiredProjectIds, Has.Count.EqualTo(16));

            // Public-API surface scoping is contract co-listing, not the reference graph at all —
            // also falls back to the full population rather than reusing the dependents closure.
            Assert.That(contractCoListing.RequiredProjectIds, Has.Count.EqualTo(16));

            // Coverage scopes 'project'/'assembly' classify each item independently from only that
            // item's own namespaces against policy-declared layers, and each item is by
            // construction exactly one project or one assembly, so this stays project-local (K=1) —
            // the one family genuinely narrower than the generic closure.
            Assert.That(aggregatedGlobalScan.RequiredProjectIds, Is.EqualTo(new[] { changedProjectId }));

            // Coverage scopes 'namespace'/'dependency_edge'/'semantic_role'/'rule_input' are
            // graph/catalog/policy-wide, not project-local ('namespace' spans multiple projects
            // because a C# namespace isn't tied to one assembly); this evidence task has no model
            // precise enough to narrow them, so they fall back to the full population like
            // CyclesGlobal.
            Assert.That(coverageGraphOrCatalogWide.RequiredProjectIds, Has.Count.EqualTo(16));
        });
    }

    [Test]
    public void EverySchemaContractFamilyReceivesAnExplicitSafeDisposition()
    {
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Linear, projectCount: 8);
        IReadOnlyList<string> allProjectIds = workload.Projects.Select(project => project.Id).ToList();
        IReadOnlyList<string> changedProjectIds = [workload.Projects[2].Id];
        IReadOnlyList<string> dependentsClosureIds = ChangedProjectScopePlanner.DependentsClosure(
            workload.Projects, workload.Edges, changedProjectIds);
        string schemaPath = Path.Combine(new ArchitectureRepositoryRootResolver().Resolve(), "schema", "dependencies.arch.schema.json");

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        IReadOnlyList<string> contractFamilies = schema.RootElement
            .GetProperty("$defs")
            .GetProperty("contracts")
            .GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .Where(name => name is not "strict" and not "audit")
            .Select(name => name[(name.IndexOf('_') + 1)..])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.That(contractFamilies, Is.Not.Empty);
        foreach (string contractFamily in contractFamilies)
        {
            EvaluatorScope scope = EvaluatorFamilyScopePlanner.PlanContractFamily(
                contractFamily, changedProjectIds, dependentsClosureIds, allProjectIds);

            Assert.Multiple(() =>
            {
                Assert.That(scope.Reason, Is.Not.Empty, contractFamily);
                Assert.That(scope.RequiredProjectIds, Is.Not.Empty, contractFamily);
                IReadOnlyList<string> expectedProjectIds = contractFamily is
                    "layers" or "external" or "external_allow_only" or "allow_only"
                    ? dependentsClosureIds
                    : allProjectIds;
                Assert.That(scope.RequiredProjectIds, Is.EqualTo(expectedProjectIds),
                    $"Schema family '{contractFamily}' must use its reviewed scope or fail closed to the full population.");
            });
        }
    }

    [TestCase("coverage:project", EvaluatorFamily.AggregatedGlobalScan)]
    [TestCase("coverage:assembly", EvaluatorFamily.AggregatedGlobalScan)]
    [TestCase("coverage:namespace", EvaluatorFamily.CoverageGraphOrCatalogWide)]
    [TestCase("coverage:dependency_edge", EvaluatorFamily.CoverageGraphOrCatalogWide)]
    [TestCase("coverage:semantic_role", EvaluatorFamily.CoverageGraphOrCatalogWide)]
    [TestCase("coverage:rule_input", EvaluatorFamily.CoverageGraphOrCatalogWide)]
    public void CoverageSubscopeFamiliesUseTheirExplicitReviewedDisposition(
        string contractFamily,
        EvaluatorFamily expectedFamily)
    {
        BenchmarkWorkloadDefinition workload = CreateWorkload(BenchmarkTopologyShape.Linear, projectCount: 8);
        IReadOnlyList<string> allProjectIds = workload.Projects.Select(project => project.Id).ToList();
        IReadOnlyList<string> changedProjectIds = [workload.Projects[2].Id];
        IReadOnlyList<string> dependentsClosureIds = ChangedProjectScopePlanner.DependentsClosure(
            workload.Projects, workload.Edges, changedProjectIds);

        EvaluatorScope scope = EvaluatorFamilyScopePlanner.PlanContractFamily(
            contractFamily, changedProjectIds, dependentsClosureIds, allProjectIds);

        Assert.Multiple(() =>
        {
            Assert.That(scope.Family, Is.EqualTo(expectedFamily));
            Assert.That(scope.RequiredProjectIds, Is.EqualTo(
                expectedFamily == EvaluatorFamily.AggregatedGlobalScan
                    ? changedProjectIds
                    : allProjectIds));
            Assert.That(scope.Reason, Is.Not.Empty);
        });
    }

    private static BenchmarkWorkloadDefinition CreateWorkload(BenchmarkTopologyShape shape, int projectCount) =>
        BenchmarkWorkloadGenerator.Create(
            $"synthetic-scope-plan-{shape.ToString().ToLowerInvariant()}-{projectCount}",
            shape,
            new BenchmarkDimensionSet { ProjectCount = projectCount, ReferencesPerProject = 0 });

    private static ChangedInput OwnedSourceFile(string inputId, string projectId) => new()
    {
        InputId = inputId,
        Kind = ChangedInputKind.ProjectOwnedSourceFile,
        OwningProjectIds = [projectId],
    };
}
