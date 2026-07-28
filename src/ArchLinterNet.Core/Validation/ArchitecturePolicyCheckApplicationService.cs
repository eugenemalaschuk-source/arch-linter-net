using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

internal sealed class ArchitecturePolicyCheckApplicationService(
    IArchitecturePolicyDocumentLoader policyDocumentLoader) : IArchitecturePolicyCheckApplicationService
{
    public PolicyCheckOutcome Check(string policyPath)
    {
        _ = policyDocumentLoader.Load(policyPath);

        return new PolicyCheckOutcome(
            [
                "root-schema-and-version",
                "imports-and-composition",
                "contract-identities-and-cross-references",
                "static-selectors-and-configuration",
            ],
            [
                new PolicyCheckDeferredCheck(
                    "architecture-evaluation",
                    "Architecture compliance requires project, assembly, or source facts and was not evaluated."),
            ]);
    }
}
