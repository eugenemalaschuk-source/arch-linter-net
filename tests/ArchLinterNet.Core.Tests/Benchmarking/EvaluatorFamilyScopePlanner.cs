namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Representative contract/evaluator families for issue #503's "do not assume a single graph
/// direction is sufficient for all contracts" requirement. Grounded in the actual checker
/// implementations under <c>src/ArchLinterNet.Core/Execution/</c>, not assumed:
/// <list type="bullet">
/// <item><see cref="ReferenceGraphLocal"/> — <c>LayerChecker</c>, <c>ExternalDependencyChecker</c>
/// (`external`/`external_allow_only`), and <c>AllowOnlyChecker</c> (`allow_only`) each scan only the
/// changed project's own layer's own outgoing references
/// (<c>context.FindTypesInLayer(sourceLayer)</c>). But the violation verdict for each reference is
/// decided by <c>ArchitectureNamespaceViolationFinder.MatchReference</c>, which classifies the
/// *target* type (namespace/role/expression facts via <c>ArchitectureLayerTypeMatcher.Matches</c>),
/// not the source. If an unchanged project A references a type in changed project B, and B's change
/// alters that type's own classification (namespace, role attribute, or CEL-evaluated metadata), A's
/// already-passing layer check can flip to a violation even though A itself did not change. The safe
/// bound therefore is B's transitive *dependents* — the same direction
/// <see cref="ChangedProjectScopePlanner"/> already computes — not the changed project alone; a
/// per-type target-fact invalidation model could narrow this further, but this evidence task does not
/// build one.</item>
/// <item><see cref="CyclesGlobal"/> — <c>CycleChecker</c> builds one shared inter-layer edge graph
/// across every layer named by the contract (<c>CollectCycleEdgesForLayer</c> populates one
/// <c>state.Graph</c>) and runs global cycle detection once over it
/// (<c>ArchitectureCycleDetector.FindCycles(state.Graph)</c>). A reference edge changed by any project
/// can flip cycle membership for any layer sharing that graph, so even the dependents closure is not a
/// safe superset — the safe bound is every project whose layer participates in the same cycle
/// contract, which this evidence task approximates conservatively as the full project population
/// absent a modeled layer-membership graph.</item>
/// <item><see cref="ContractCoListing"/> — <c>PublicApiSurfaceChecker</c> only scans assemblies
/// explicitly named in one contract's <c>Assemblies</c> list; a downstream consumer not co-listed in
/// that same contract is unaffected even if it depends on the changed project through the ordinary
/// project-reference graph. This is why <c>ChangedProjectScopePlanner</c> treats
/// <see cref="ChangedInputKind.ApiSnapshotOrBaselineChange"/> as <see cref="ScopeDisposition.UnmappableFallback"/>
/// rather than a reference-graph closure: the real relationship is contract membership, not
/// reachability, and this task does not model contract-to-assemblies membership.</item>
/// <item><see cref="AggregatedGlobalScan"/> — architecture-coverage's `project` and `assembly` scopes
/// (<c>schema/dependencies.arch.schema.json</c> coverage `scope` enum) classify each item
/// independently from only that item's own namespaces against policy-declared layers
/// (<c>CheckProjectCoverageContract</c>/<c>CheckAssemblyCoverageContract</c>), and each item is by
/// construction exactly one project or one assembly — so the correctness-relevant scope is the changed
/// project alone. Today's execution re-scans the whole solution and returns one combined findings list
/// regardless, which is a separate execution-model limitation this evidence task records but does not
/// resolve. `namespace` does **not** belong in this family — see
/// <see cref="CoverageGraphOrCatalogWide"/>.</item>
/// <item><see cref="CoverageGraphOrCatalogWide"/> — the coverage scope enum's remaining four values do
/// not share <see cref="AggregatedGlobalScan"/>'s per-item-local shape. `namespace`
/// (<c>ArchitectureCoverageInventory.Build</c>) groups <c>session.TypeIndex.AllTypes()</c> — the
/// *whole solution's* types — by namespace string and picks one representative type; a C# namespace is
/// not tied to one assembly, so a shared namespace entry can legitimately span types owned by several
/// projects, and changing any one of them can change that entry's coverage classification. This
/// evidence task originally grouped `namespace` with `project`/`assembly` as project-local; that was
/// wrong, since only `project` and `assembly` are inherently single-project/assembly by construction.
/// `dependency_edge` (<c>ArchitectureDependencyEdgeCoverageService.Check</c>) evaluates declared
/// layer-name pairs against edges observed across the *whole* coverage inventory, so any project
/// touching either layer's namespace membership can change the result; `semantic_role`
/// (<c>ArchitectureSemanticCoverageService.BuildSummary</c>) iterates every type from
/// <c>TypeIndex.AllTypes()</c> and classifies each via the shared role catalog, which — like the
/// <see cref="ReferenceGraphLocal"/> case — is not proven free of cross-project classification
/// dependencies; `rule_input` operates over contract ids from policy, not project code, so it is
/// policy-level rather than project-scoped. This evidence task does not have a graph/catalog model
/// precise enough to bound any of the four below the full project population.</item>
/// </list>
/// This taxonomy intentionally covers a representative subset, not all ~34 contract families in
/// <c>schema/dependencies.arch.schema.json</c>; families outside this subset are not claimed to be
/// safely bounded by any of the models below.
/// </summary>
internal enum EvaluatorFamily
{
    ReferenceGraphLocal,
    CyclesGlobal,
    ContractCoListing,
    AggregatedGlobalScan,
    CoverageGraphOrCatalogWide,
    UnanalyzedSafeFallback,
}

internal sealed record EvaluatorScope
{
    public required EvaluatorFamily Family { get; init; }

    public required IReadOnlyList<string> RequiredProjectIds { get; init; }

    public required string Reason { get; init; }
}

/// <summary>
/// Computes the per-evaluator-family required re-evaluation scope for a set of changed projects,
/// given both the changed projects themselves and the reference-graph transitive dependents closure
/// <see cref="ChangedProjectScopePlanner"/> already produces. It demonstrates that no single closure
/// model is sufficient across families: <see cref="AggregatedGlobalScan"/> needs strictly less than
/// the dependents closure, while <see cref="CyclesGlobal"/>, <see cref="ContractCoListing"/>, and
/// <see cref="CoverageGraphOrCatalogWide"/> need a bound the dependents closure does not safely
/// provide at all — and, after review, <see cref="ReferenceGraphLocal"/> turned out to need the full
/// dependents closure too, not the changed project alone as an earlier revision of this evidence
/// claimed.
/// </summary>
internal static class EvaluatorFamilyScopePlanner
{
    public static EvaluatorScope PlanContractFamily(
        string contractFamily,
        IReadOnlyList<string> changedProjectIds,
        IReadOnlyList<string> dependentsClosureIds,
        IReadOnlyList<string> allProjectIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractFamily);

        return contractFamily switch
        {
            "layers" or "external" or "external_allow_only" or "allow_only" => Plan(
                EvaluatorFamily.ReferenceGraphLocal,
                changedProjectIds,
                dependentsClosureIds,
                allProjectIds),
            "cycles" => Plan(
                EvaluatorFamily.CyclesGlobal,
                changedProjectIds,
                dependentsClosureIds,
                allProjectIds),
            "public_api_surface" => Plan(
                EvaluatorFamily.ContractCoListing,
                changedProjectIds,
                dependentsClosureIds,
                allProjectIds),
            "coverage:project" or "coverage:assembly" => Plan(
                EvaluatorFamily.AggregatedGlobalScan,
                changedProjectIds,
                dependentsClosureIds,
                allProjectIds),
            "coverage:namespace" or "coverage:dependency_edge" or "coverage:semantic_role" or "coverage:rule_input" => Plan(
                EvaluatorFamily.CoverageGraphOrCatalogWide,
                changedProjectIds,
                dependentsClosureIds,
                allProjectIds),
            _ => new EvaluatorScope
            {
                Family = EvaluatorFamily.UnanalyzedSafeFallback,
                RequiredProjectIds = allProjectIds,
                Reason = $"Contract family '{contractFamily}' is not represented by the reviewed #503 " +
                    "checker taxonomy. Until its checker/fact dependencies are analyzed, the safe " +
                    "disposition is full-population fallback; no reference-graph direction is assumed.",
            },
        };
    }

    public static EvaluatorScope Plan(
        EvaluatorFamily family,
        IReadOnlyList<string> changedProjectIds,
        IReadOnlyList<string> dependentsClosureIds,
        IReadOnlyList<string> allProjectIds)
    {
        return family switch
        {
            EvaluatorFamily.ReferenceGraphLocal => new EvaluatorScope
            {
                Family = family,
                RequiredProjectIds = dependentsClosureIds,
                Reason = "LayerChecker/ExternalDependencyChecker/AllowOnlyChecker scan only the changed " +
                    "project's own outgoing references, but the verdict for each reference depends on the " +
                    "target type's own classification (ArchitectureNamespaceViolationFinder.MatchReference); a " +
                    "change to the target project can flip an unchanged dependent's passing result, so the safe " +
                    "bound is the changed project's transitive dependents, not the changed project alone.",
            },
            EvaluatorFamily.CyclesGlobal => new EvaluatorScope
            {
                Family = family,
                RequiredProjectIds = allProjectIds,
                Reason = "CycleChecker builds one shared inter-layer edge graph across every layer named by the " +
                    "contract and runs global cycle detection once over it; even the dependents closure is not a " +
                    "safe superset of the projects whose cycle membership could flip, so this evidence task " +
                    "conservatively falls back to the full project population absent a modeled layer-membership " +
                    "graph.",
            },
            EvaluatorFamily.ContractCoListing => new EvaluatorScope
            {
                Family = family,
                RequiredProjectIds = allProjectIds,
                Reason = "PublicApiSurfaceChecker scopes to the assemblies explicitly co-listed in one contract's " +
                    "'assemblies' field, a contract-membership relationship this task does not model; falls back " +
                    "to the full project population rather than assuming the reference graph applies.",
            },
            EvaluatorFamily.AggregatedGlobalScan => new EvaluatorScope
            {
                Family = family,
                RequiredProjectIds = changedProjectIds,
                Reason = "Coverage scopes 'project'/'assembly' classify each item independently from only that " +
                    "item's own namespaces against policy-declared layers, and each item is by construction " +
                    "exactly one project or one assembly, so the correctness-relevant scope is the changed " +
                    "project alone; today's execution re-scans the whole solution regardless, which is a " +
                    "separate execution-model limitation this evidence task records but does not resolve. " +
                    "'namespace' is intentionally excluded from this family (see CoverageGraphOrCatalogWide).",
            },
            EvaluatorFamily.CoverageGraphOrCatalogWide => new EvaluatorScope
            {
                Family = family,
                RequiredProjectIds = allProjectIds,
                Reason = "Coverage scopes 'namespace' (ArchitectureCoverageInventory.Build groups the whole " +
                    "solution's types by namespace string and picks one representative type; a namespace is not " +
                    "tied to one assembly, so a shared namespace entry can span multiple projects), " +
                    "'dependency_edge' (evaluated over the whole coverage inventory's observed edges), " +
                    "'semantic_role' (iterates every type via the shared role catalog, with the same unproven " +
                    "cross-project classification risk as ReferenceGraphLocal), and 'rule_input' (policy-level, " +
                    "not project-scoped) do not share AggregatedGlobalScan's per-item-local shape; this evidence " +
                    "task has no graph/catalog model precise enough to bound any of them below the full project " +
                    "population.",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown evaluator family."),
        };
    }
}
