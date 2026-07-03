using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution.Abstractions;
using ArchLinterNet.Core.Scanning;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

internal sealed class AsmdefValidationService(
    IArchitecturePolicyDocumentLoader policyDocumentLoader,
    IArchitectureRepositoryRootResolver repositoryRootResolver,
    IArchitectureAsmdefScanner asmdefScanner)
    : IAsmdefValidationService
{
    public AsmdefValidationOutcome Validate(AsmdefValidationRequest request)
    {
        ArchitectureContractDocument document = policyDocumentLoader.Load(request.PolicyPath);
        string repositoryRoot = repositoryRootResolver.ResolveFrom(request.PolicyPath);

        List<ArchitectureViolation> allViolations = new();

        foreach (ArchitectureAsmdefContract contract in document.Contracts.StrictAsmdef)
        {
            allViolations.AddRange(asmdefScanner.FindAsmdefViolations(
                contract.Name,
                contract.Id,
                repositoryRoot,
                contract));
        }

        return new AsmdefValidationOutcome(
            Passed: allViolations.Count == 0,
            Violations: allViolations);
    }
}
