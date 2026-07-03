using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Abstractions;

public interface IArchitectureBaselineGenerator
{
    ArchitectureBaselineDocument Generate(
        ArchitectureContractDocument policyDocument,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string reason = "generated baseline");

    string Serialize(ArchitectureBaselineDocument document);
}
