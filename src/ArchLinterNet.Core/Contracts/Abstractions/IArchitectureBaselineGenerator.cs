using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts.Abstractions;

public interface IArchitectureBaselineGenerator
{
    ArchitectureBaselineDocument Generate(
        ArchitectureContractDocument policyDocument,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string reason = "generated baseline");

    ArchitectureBaselineDocument BuildFromEntries(IReadOnlyList<ArchitectureBaselineComparisonEntry> entries);

    string Serialize(ArchitectureBaselineDocument document);
}
