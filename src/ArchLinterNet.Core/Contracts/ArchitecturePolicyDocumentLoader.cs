using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Contracts;

public interface IArchitecturePolicyDocumentLoader
{
    ArchitectureContractDocument Load(string policyPath);
}

public sealed class ArchitecturePolicyDocumentLoader : IArchitecturePolicyDocumentLoader
{
    private static readonly string[] _implementedCoverageScopes =
        { "namespace", "rule_input", "project", "assembly", "dependency_edge" };

    private readonly IArchitectureFileSystem _fileSystem;

    public ArchitecturePolicyDocumentLoader()
        : this(ArchitectureFileSystem.Real)
    {
    }

    public ArchitecturePolicyDocumentLoader(IArchitectureFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public ArchitectureContractDocument Load(string policyPath)
    {
        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(policyPath, _fileSystem);
        ValidateImplementedCoverageScopes(document);
        return document;
    }

    private static void ValidateImplementedCoverageScopes(ArchitectureContractDocument document)
    {
        List<ArchitectureCoverageContract> unsupported = document.Contracts.StrictCoverage
            .Concat(document.Contracts.AuditCoverage)
            .Where(contract => !_implementedCoverageScopes.Contains(contract.Scope, StringComparer.Ordinal))
            .ToList();

        if (unsupported.Count == 0)
        {
            return;
        }

        string details = string.Join(", ", unsupported.Select(contract => $"{contract.Name} ({contract.Scope})"));
        throw new InvalidOperationException(
            "Only coverage contracts with scope 'namespace', 'rule_input', 'project', 'assembly', or " +
            $"'dependency_edge' are implemented right now. Unsupported coverage contract scopes: {details}.");
    }
}
