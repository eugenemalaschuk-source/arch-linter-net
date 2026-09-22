namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Representative contract/evaluator families for issue #503's "do not assume a single graph
/// direction is sufficient for all contracts" requirement. Grounded in the actual checker
/// implementations under <c>src/ArchLinterNet.Core/Execution/Checkers/</c>, not assumed:
/// <list type="bullet">
/// <item><see cref="ReferenceGraphLocal"/> — <c>LayerChecker</c>, <c>ExternalDependencyChecker</c>
/// (`external`/`external_allow_only`), and <c>AllowOnlyChecker</c> (`allow_only`) each evaluate only
/// the changed project's own layer's own outgoing references
/// (<c>context.FindTypesInLayer(sourceLayer)</c> then that layer's own reference/IL scan). No other
/// project's types are ever enumerated, so re-evaluation needs only the changed project itself.</item>
/// <item><see cref="CyclesGlobal"/> — <c>CycleChecker</c> builds one shared inter-layer edge graph
/// across every layer named by the contract (<c>CollectCycleEdgesForLayer</c> populates one
/// <c>state.Graph</c>) and runs global cycle detection once over it
/// (<c>ArchitectureCycleDetector.FindCycles(state.Graph)</c>). A reference edge changed by any project
/// can flip cycle membership for any layer sharing that graph, so the changed project's transitive
/// *dependents* closure is not a safe superset here — the safe bound is every project whose layer
/// participates in the same cycle contract, which this evidence task approximates conservatively as
/// the full project population absent a modeled layer-membership graph.</item>
/// <item><see cref="ContractCoListing"/> — <c>PublicApiSurfaceChecker</c> only scans assemblies
/// explicitly named in one contract's <c>Assemblies</c> list; a downstream consumer not co-listed in
/// that same contract is unaffected even if it depends on the changed project through the ordinary
/// project-reference graph. This is why <c>ChangedProjectScopePlanner</c> treats
/// <see cref="ChangedInputKind.ApiSnapshotOrBaselineChange"/> as <see cref="ScopeDisposition.UnmappableFallback"/>
/// rather than a reference-graph closure: the real relationship is contract membership, not
/// reachability, and this task does not model contract-to-assemblies membership.</item>
/// <item><see cref="AggregatedGlobalScan"/> — the architecture-coverage checks classify each
/// project/namespace independently from only that item's own namespaces
/// (<c>GetAssemblyNamespaces(resolvedAssembly)</c>), so the *correctness-relevant* scope is the
/// changed project alone; the *current execution model* re-scans the full solution list every run and
/// returns one combined findings list, so today's implementation cannot exploit that narrower scope
/// without re-architecting the coverage check itself. This evidence task records the narrower logical
/// scope and flags the execution-model gap rather than conflating the two.</item>
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
}

internal sealed record EvaluatorScope
{
    public required EvaluatorFamily Family { get; init; }

    public required IReadOnlyList<string> RequiredProjectIds { get; init; }

    public required string Reason { get; init; }
}

/// <summary>
/// Computes the per-evaluator-family required re-evaluation scope for a set of changed projects,
/// given the reference-graph transitive dependents closure <see cref="ChangedProjectScopePlanner"/>
/// already produces. It demonstrates that no single closure model is sufficient across families: some
/// need strictly less than the dependents closure (<see cref="EvaluatorFamily.ReferenceGraphLocal"/>,
/// <see cref="EvaluatorFamily.AggregatedGlobalScan"/>), and one needs a bound the dependents closure
/// does not safely provide at all (<see cref="EvaluatorFamily.CyclesGlobal"/>).
/// </summary>
internal static class EvaluatorFamilyScopePlanner
{
    public static EvaluatorScope Plan(
        EvaluatorFamily family,
        IReadOnlyList<string> changedProjectIds,
        IReadOnlyList<string> allProjectIds)
    {
        return family switch
        {
            EvaluatorFamily.ReferenceGraphLocal => new EvaluatorScope
            {
                Family = family,
                RequiredProjectIds = changedProjectIds,
                Reason = "LayerChecker/ExternalDependencyChecker/AllowOnlyChecker evaluate only the changed " +
                    "project's own layer's own outgoing references; no other project's types are enumerated, so " +
                    "re-evaluation needs only the changed project(s) themselves, not their dependents.",
            },
            EvaluatorFamily.CyclesGlobal => new EvaluatorScope
            {
                Family = family,
                RequiredProjectIds = allProjectIds,
                Reason = "CycleChecker builds one shared inter-layer edge graph across every layer named by the " +
                    "contract and runs global cycle detection once over it; the changed project's reference-graph " +
                    "dependents closure is not a safe superset of the projects whose cycle membership could flip, " +
                    "so this evidence task conservatively falls back to the full project population absent a " +
                    "modeled layer-membership graph.",
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
                Reason = "Architecture-coverage checks classify each project/namespace independently from only " +
                    "that item's own namespaces, so the correctness-relevant scope is the changed project alone; " +
                    "today's execution re-scans the whole solution regardless, which is a separate execution-model " +
                    "limitation this evidence task records but does not resolve.",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown evaluator family."),
        };
    }
}
