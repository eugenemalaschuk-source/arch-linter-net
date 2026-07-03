using ArchLinterNet.Core.Asmdef.Abstractions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution.Abstractions;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Asmdef;

public sealed class AsmdefValidationService : IAsmdefValidationService
{
    private readonly IArchitecturePolicyDocumentLoader _documentLoader;
    private readonly IArchitectureRepositoryRootResolver _repositoryRootResolver;
    private readonly IArchitectureAsmdefScanner _asmdefScanner;

    public AsmdefValidationService(
        IArchitecturePolicyDocumentLoader documentLoader,
        IArchitectureRepositoryRootResolver repositoryRootResolver,
        IArchitectureAsmdefScanner asmdefScanner)
    {
        _documentLoader = documentLoader;
        _repositoryRootResolver = repositoryRootResolver;
        _asmdefScanner = asmdefScanner;
    }

    public AsmdefValidationOutcome Validate(AsmdefValidationRequest request)
    {
        ArchitectureContractDocument document = _documentLoader.Load(request.PolicyPath);
        string repositoryRoot = _repositoryRootResolver.ResolveFrom(request.PolicyPath);

        List<ArchitectureViolation> violations = new();
        foreach (ArchitectureAsmdefContract contract in document.Contracts.StrictAsmdef)
        {
            violations.AddRange(_asmdefScanner.FindAsmdefViolations(
                contract.Name,
                contract.Id,
                repositoryRoot,
                contract));
        }

        return new AsmdefValidationOutcome { Violations = violations };
    }
}
