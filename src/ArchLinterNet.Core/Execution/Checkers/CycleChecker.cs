using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

// A candidate observed while building the layer reference graph, kept with the edge it came from so
// the session can decide afterwards whether that edge actually participates in a detected cycle.
internal sealed record CycleCandidateEvidence(
    string SourceLayerName,
    string TargetLayerName,
    ArchitectureBaselineCandidate Candidate);

internal static class CycleChecker
{
    // The graph and candidate evidence are returned rather than published here: appending baseline
    // candidates is session-owned mutable state, so the session wrapper does it once the cycle set
    // is known.
    internal sealed record Result(
        IReadOnlyCollection<string> Cycles,
        IReadOnlyDictionary<string, HashSet<string>> Graph,
        IReadOnlyCollection<CycleCandidateEvidence> CandidateEvidence);

    public static Result Check(
        ArchitectureCycleContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        var contractLayers = contract.Layers.ToHashSet(StringComparer.Ordinal);
        var graph = contractLayers.ToDictionary(
            layer => layer,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var cycleCandidateEvidence = new List<CycleCandidateEvidence>();

        foreach (string sourceLayerName in contract.Layers)
        {
            CollectCycleEdgesForLayer(
                contract, sourceLayerName, contractLayers, context, executionContext, graph, cycleCandidateEvidence);
        }

        IReadOnlyCollection<string> cycles = ArchitectureCycleDetector.FindCycles(graph);
        return new Result(cycles, graph, cycleCandidateEvidence);
    }

    private static void CollectCycleEdgesForLayer(
        ArchitectureCycleContract contract,
        string sourceLayerName,
        HashSet<string> contractLayers,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        Dictionary<string, HashSet<string>> graph,
        List<CycleCandidateEvidence> cycleCandidateEvidence)
    {
        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, sourceLayerName);
        Type[] sourceTypes = context.FindTypesInLayer(sourceLayer);

        foreach (Type sourceType in sourceTypes)
        {
            string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);
            string sourceAssembly = ArchitectureTypeNames.SafeAssemblyName(sourceType) ?? string.Empty;

            foreach (Type referencedType in context.ReferenceGraph.GetReferencedTypes(sourceType))
            {
                string referencedTypeName = ArchitectureTypeNames.SafeFullName(referencedType);
                string? referencedLayerName = context.ResolveContainingLayer(referencedType, contractLayers);

                if (referencedLayerName == null || referencedLayerName == sourceLayerName)
                {
                    continue;
                }

                if (executionContext.IsIgnored(
                        sourceTypeName,
                        referencedTypeName,
                        sourceAssembly: sourceAssembly,
                        targetAssembly: ArchitectureTypeNames.SafeAssemblyName(referencedType),
                        targetType: referencedTypeName,
                        targetMember: referencedTypeName,
                        observeCandidate: candidate => cycleCandidateEvidence.Add(
                            new CycleCandidateEvidence(sourceLayerName, referencedLayerName, candidate))))
                {
                    continue;
                }

                graph[sourceLayerName].Add(referencedLayerName);
            }
        }
    }
}
