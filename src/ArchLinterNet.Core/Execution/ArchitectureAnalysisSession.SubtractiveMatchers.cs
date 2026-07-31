using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// Typed inclusion/exclusion participation evidence for type/layout matchers, split out of
// ArchitectureAnalysisSession.cs to keep both files under the repository's file-size lint budget
// (make/lint.mk CS_SIZE_LINT_ERROR_LINES). Populated by
// ArchitectureAnalysisSession.TypePlacement.cs and ArchitectureAnalysisSession.LayoutConventions.cs
// / .LayoutMatching.cs as each contract that declares these matchers executes.
public sealed partial class ArchitectureAnalysisSession
{
    // Bundles per-contract layout participation state (one array slot per authored exclusion, plus
    // the single inclusion selector's own status) so the file/candidate collection methods in
    // ArchitectureAnalysisSession.LayoutMatching.cs/.LayoutConventions.cs can thread one object
    // instead of two bool[] arrays and two `out bool` parameters each.
    private sealed class LayoutExclusionTracker
    {
        public LayoutExclusionTracker(int exclusionCount)
        {
            Matched = new bool[exclusionCount];
            EvaluationFailed = new bool[exclusionCount];
        }

        public bool[] Matched { get; }

        public bool[] EvaluationFailed { get; }

        public bool InclusionMatched { get; set; }

        public bool InclusionEvaluationFailed { get; set; }
    }

    private readonly List<ArchitectureSubtractiveMatcherParticipation> _subtractiveMatcherParticipation = new();

    public IReadOnlyList<ArchitectureSubtractiveMatcherParticipation> SubtractiveMatcherParticipation
        => _subtractiveMatcherParticipation;

    private void RecordSubtractiveMatcherParticipation(
        IArchitectureContract contract,
        string field,
        int? index,
        bool matched,
        bool evaluationFailed = false,
        ArchitectureSelectorParticipationKind kind = ArchitectureSelectorParticipationKind.Exclusion)
    {
        _subtractiveMatcherParticipation.Add(new ArchitectureSubtractiveMatcherParticipation(
            contract.Id ?? contract.Name, contract.Name, field, index, matched)
        {
            PolicyLocation = ItemLocation(contract, field, index),
            EvaluationFailed = evaluationFailed,
            Kind = kind,
            Mode = ResolveSelectorParticipationMode(contract)
        });
    }

    private ArchitecturePolicySourceLocation? ItemLocation(IArchitectureContract contract, string field, int? index)
    {
        ArchitecturePolicySourceLocation? contractLocation = Document.Provenance.LocationFor(contract);
        if (contractLocation is null)
        {
            return null;
        }

        string path = ArchitecturePolicyProvenancePath.AppendProperty(contractLocation.YamlPath, field);
        if (index is int itemIndex)
        {
            path = ArchitecturePolicyProvenancePath.AppendIndex(path, itemIndex);
        }
        return Document.Provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location)
            ? location
            : contractLocation with { YamlPath = path };
    }

    private ArchitectureSelectorParticipationMode ResolveSelectorParticipationMode(IArchitectureContract contract)
    {
        string? group = ResolveContractGroup(contract);
        if (group is null)
        {
            return ArchitectureSelectorParticipationMode.Unknown;
        }

        return group.StartsWith("audit_", StringComparison.Ordinal)
            ? ArchitectureSelectorParticipationMode.Audit
            : ArchitectureSelectorParticipationMode.Strict;
    }
}
