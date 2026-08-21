using ArchLinterNet.Core.Contracts.Abstractions;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class PolicyWeakeningSeverityValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Analysis.PolicyWeakening is not ("error" or "warn" or "off"))
        {
            throw new InvalidOperationException(
                $"Invalid analysis.policy_weakening: {document.Analysis.PolicyWeakening}. Use 'error', 'warn', or 'off'.");
        }
    }
}
