using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
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
        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(contractPath);

        string repositoryRoot = new ArchitectureRepositoryRootResolver().ResolveFrom(contractPath);

        List<Core.Model.ArchitectureViolation> allViolations = new();

        foreach (ArchitectureAsmdefContract contract in document.Contracts.StrictAsmdef)
        {
            allViolations.AddRange(new ArchitectureAsmdefScanner().FindAsmdefViolations(
                contract.Name,
                contract.Id,
                repositoryRoot,
                contract));
        }

        violations = allViolations;
        return allViolations.Count == 0;
    }

}
