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
        BenchmarkWorkloadDefinition workload = CreateWorkload(shape, projectCount: 12);
        string leafProjectId = workload.Projects[0].Id;

        ScopePlan plan = ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [OwnedSourceFile("leaf-change", leafProjectId)]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.AffectedProjectIds, Is.EqualTo(new[] { leafProjectId }), shape.ToString());
            Assert.That(plan.AffectedProjectCount, Is.EqualTo(1), shape.ToString());
            Assert.That(plan.IsFullFallback, Is.False, shape.ToString());
        });
    }

    [TestCaseSource(nameof(_executableShapes))]
    public void SharedFoundationProjectChange_AffectsEveryProject(BenchmarkTopologyShape shape)
    {
        BenchmarkWorkloadDefinition workload = CreateWorkload(shape, projectCount: 12);
        string sharedProjectId = workload.Projects[^1].Id;

        ScopePlan plan = ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [OwnedSourceFile("shared-change", sharedProjectId)]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.AffectedProjectCount, Is.EqualTo(12), shape.ToString());
            Assert.That(plan.AffectedScopeRatio, Is.EqualTo(1.0m), shape.ToString());
        });
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

        ScopePlan genericPlan = ChangedProjectScopePlanner.Plan(
            workload.Projects,
            workload.Edges,
            [OwnedSourceFile("middle-change", changedProjectId)]);
        Assert.That(genericPlan.AffectedProjectCount, Is.EqualTo(9), "generic dependents-closure baseline");

        EvaluatorScope referenceGraphLocal = EvaluatorFamilyScopePlanner.Plan(
            EvaluatorFamily.ReferenceGraphLocal, [changedProjectId], allProjectIds);
        EvaluatorScope cyclesGlobal = EvaluatorFamilyScopePlanner.Plan(
            EvaluatorFamily.CyclesGlobal, [changedProjectId], allProjectIds);
        EvaluatorScope contractCoListing = EvaluatorFamilyScopePlanner.Plan(
            EvaluatorFamily.ContractCoListing, [changedProjectId], allProjectIds);
        EvaluatorScope aggregatedGlobalScan = EvaluatorFamilyScopePlanner.Plan(
            EvaluatorFamily.AggregatedGlobalScan, [changedProjectId], allProjectIds);

        Assert.Multiple(() =>
        {
            // Layers/external/allow_only checkers scan only the changed project's own outgoing
            // references: strictly narrower than the generic dependents closure (1 < 9).
            Assert.That(referenceGraphLocal.RequiredProjectIds, Is.EqualTo(new[] { changedProjectId }));

            // Cycle detection shares one graph across every layer in the contract: the dependents
            // closure is not a safe bound, so this evidence task falls back to the full population
            // (16), which is wider than the generic closure (9), not narrower.
            Assert.That(cyclesGlobal.RequiredProjectIds, Has.Count.EqualTo(16));

            // Public-API surface scoping is contract co-listing, not the reference graph at all —
            // also falls back to the full population rather than reusing the dependents closure.
            Assert.That(contractCoListing.RequiredProjectIds, Has.Count.EqualTo(16));

            // Coverage classification is per-item and project-local, same as ReferenceGraphLocal.
            Assert.That(aggregatedGlobalScan.RequiredProjectIds, Is.EqualTo(new[] { changedProjectId }));
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
