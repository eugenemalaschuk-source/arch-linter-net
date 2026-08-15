using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// Typed inclusion/exclusion participation evidence for type/layout matchers. The list is
// session-owned run state; TypePlacementChecker and LayoutConventionChecker append to it through
// ArchitectureCheckerContext's recording port as each contract that declares these matchers
// executes, so record order stays purely a function of contract-family execution order.
internal sealed class ArchitectureSubtractiveMatcherParticipationRecorder
{
    private readonly ArchitectureAnalysisSession _session;

    private readonly List<ArchitectureSubtractiveMatcherParticipation> _subtractiveMatcherParticipation = new();

    public ArchitectureSubtractiveMatcherParticipationRecorder(ArchitectureAnalysisSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public IReadOnlyList<ArchitectureSubtractiveMatcherParticipation> Participations
        => _subtractiveMatcherParticipation;

    public void Record(
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
        ArchitecturePolicySourceLocation? contractLocation = _session.Document.Provenance.LocationFor(contract);
        if (contractLocation is null)
        {
            return null;
        }

        string path = ArchitecturePolicyProvenancePath.AppendProperty(contractLocation.YamlPath, field);
        if (index is int itemIndex)
        {
            path = ArchitecturePolicyProvenancePath.AppendIndex(path, itemIndex);
        }
        return _session.Document.Provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location)
            ? location
            : contractLocation with { YamlPath = path };
    }

    private ArchitectureSelectorParticipationMode ResolveSelectorParticipationMode(IArchitectureContract contract)
    {
        string? group = _session.ResolveContractGroup(contract);
        if (group is null)
        {
            return ArchitectureSelectorParticipationMode.Unknown;
        }

        return group.StartsWith("audit_", StringComparison.Ordinal)
            ? ArchitectureSelectorParticipationMode.Audit
            : ArchitectureSelectorParticipationMode.Strict;
    }
}
