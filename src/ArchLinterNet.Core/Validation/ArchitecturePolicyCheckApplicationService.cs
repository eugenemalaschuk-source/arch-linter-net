using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

internal sealed class ArchitecturePolicyCheckApplicationService(
    IArchitecturePolicyDocumentLoader policyDocumentLoader) : IArchitecturePolicyCheckApplicationService
{
    public PolicyCheckOutcome Check(string policyPath)
    {
        try
        {
            ArchitectureContractDocument document = policyDocumentLoader.Load(policyPath);

            return new PolicyCheckOutcome(
                [
                    "root-schema-and-version",
                    "imports-and-composition",
                    "contract-identities-and-cross-references",
                    "static-selectors-and-configuration",
                ],
                BuildDeferredChecks(document));
        }
        catch (ArchitecturePolicyImportException exception)
        {
            return PolicyCheckOutcome.Invalid(new PolicyCheckFailure(
                exception.Message,
                exception.Category.ToString(),
                exception.Diagnostic));
        }
        catch (ArchitecturePolicyValidationException exception)
        {
            return PolicyCheckOutcome.Invalid(new PolicyCheckFailure(
                exception.Message,
                exception.Diagnostic.Kind.ToString(),
                exception.Diagnostic));
        }
    }

    private static IReadOnlyCollection<PolicyCheckDeferredCheck> BuildDeferredChecks(
        ArchitectureContractDocument document)
    {
        if (document.ClassificationPathDeferred is not { } deferred)
        {
            return Array.Empty<PolicyCheckDeferredCheck>();
        }

        return
        [
            new PolicyCheckDeferredCheck(
                "classification-path",
                $"{deferred.DeclaredEntryCount} classification.path declaration(s) require source facts and were not evaluated.",
                deferred.PolicyLocations),
        ];
    }
}
