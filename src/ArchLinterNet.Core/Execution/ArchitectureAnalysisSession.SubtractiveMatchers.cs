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
    private readonly List<ArchitectureSubtractiveMatcherParticipation> _subtractiveMatcherParticipation = new();

    public IReadOnlyList<ArchitectureSubtractiveMatcherParticipation> SubtractiveMatcherParticipation
        => _subtractiveMatcherParticipation;

    private void RecordSubtractiveMatcherParticipation(
        IArchitectureContract contract,
        string field,
        int index,
        bool matched,
        bool evaluationFailed = false,
        ArchitectureSelectorParticipationKind kind = ArchitectureSelectorParticipationKind.Exclusion)
    {
        _subtractiveMatcherParticipation.Add(new ArchitectureSubtractiveMatcherParticipation(
            contract.Id ?? contract.Name, contract.Name, field, index, matched)
        {
            PolicyLocation = ItemLocation(contract, field, index),
            EvaluationFailed = evaluationFailed,
            Kind = kind
        });
    }

    private ArchitecturePolicySourceLocation? ItemLocation(IArchitectureContract contract, string field, int index)
    {
        ArchitecturePolicySourceLocation? contractLocation = Document.Provenance.LocationFor(contract);
        if (contractLocation is null)
        {
            return null;
        }

        string path = ArchitecturePolicyProvenancePath.AppendIndex(
            ArchitecturePolicyProvenancePath.AppendProperty(contractLocation.YamlPath, field), index);
        return Document.Provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location)
            ? location
            : contractLocation with { YamlPath = path };
    }
}
