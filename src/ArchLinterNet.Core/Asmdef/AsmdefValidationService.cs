using ArchLinterNet.Core.Asmdef.Abstractions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution.Abstractions;
using ArchLinterNet.Core.Scanning.Abstractions;

namespace ArchLinterNet.Core.Asmdef;

public sealed class AsmdefValidationService(
    IArchitecturePolicyDocumentLoader documentLoader,
    IArchitectureRepositoryRootResolver repositoryRootResolver,
    IArchitectureAsmdefScanner asmdefScanner)
    : IAsmdefValidationService
{
    public AsmdefValidationOutcome Validate(AsmdefValidationRequest request)
    {
        ArchitectureContractDocument document = documentLoader.Load(request.PolicyPath);
        string repositoryRoot = repositoryRootResolver.ResolveFrom(request.PolicyPath);

        List<ArchitectureViolation> violations = new();
        foreach (ArchitectureAsmdefContract contract in document.Contracts.StrictAsmdef)
        {
            violations.AddRange(asmdefScanner.FindAsmdefViolations(
                contract.Name,
                contract.Id,
                repositoryRoot,
                contract));
        }

        return new AsmdefValidationOutcome { Violations = violations };
    }
}
