using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Unity;

public sealed class AsmdefValidator
{
    public bool Validate(string contractPath)
    {
        return Validate(contractPath, out _);
    }

    public bool Validate(string contractPath, out IReadOnlyCollection<Core.Model.ArchitectureViolation> violations)
    {
        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(contractPath);

        string repositoryRoot = ResolveRepositoryRoot(contractPath);

        List<Core.Model.ArchitectureViolation> allViolations = new();

        foreach (ArchitectureAsmdefContract contract in document.Contracts.StrictAsmdef)
        {
            allViolations.AddRange(ArchitectureAsmdefScanner.FindAsmdefViolations(
                contract.Name,
                repositoryRoot,
                contract));
        }

        violations = allViolations;
        return allViolations.Count == 0;
    }

    private static string ResolveRepositoryRoot(string contractPath)
    {
        string? contractDir = Path.GetDirectoryName(contractPath);
        if (string.IsNullOrEmpty(contractDir))
        {
            return Directory.GetCurrentDirectory();
        }

        if (string.Equals(Path.GetFileName(contractDir), "architecture", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(contractDir) ?? contractDir;
        }

        return contractDir;
    }
}
