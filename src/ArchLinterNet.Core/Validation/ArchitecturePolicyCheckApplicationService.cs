using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Families;
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
            ArchitectureContractDocument document = policyDocumentLoader.Load(policyPath, validateEffectiveSchema: true);
            PolicyCheckFailure? snapshotFailure = FindSnapshotFailure(document);
            if (snapshotFailure is not null)
            {
                return PolicyCheckOutcome.Invalid(snapshotFailure);
            }

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

    private static PolicyCheckFailure? FindSnapshotFailure(ArchitectureContractDocument document)
    {
        foreach (ArchitecturePublicApiSurfaceContract contract in document.Contracts.StrictPublicApiSurface
                     .Concat(document.Contracts.AuditPublicApiSurface)
                     .Where(contract => contract.ApiSnapshotErrorKind is PublicApiSnapshotErrorKind.ParseError or PublicApiSnapshotErrorKind.OwnershipError))
        {
            ArchitecturePolicySourceLocation? location = document.Provenance.LocationFor(contract);
            ArchitecturePolicyDiagnostic? diagnostic = location is null ? null : new ArchitecturePolicyDiagnostic(
                ArchitecturePolicyDiagnosticKind.SemanticValidation,
                location,
                Array.Empty<ArchitecturePolicySourceLocation>(),
                location.Source.ImportChain);
            return new PolicyCheckFailure(contract.ApiSnapshotError!, contract.ApiSnapshotErrorKind.ToString(), diagnostic);
        }

        return null;
    }

    private static IReadOnlyCollection<PolicyCheckDeferredCheck> BuildDeferredChecks(
        ArchitectureContractDocument document)
    {
        var checks = new List<PolicyCheckDeferredCheck>();
        if (document.ClassificationPathDeferred is { } classificationPath)
        {
            checks.Add(new PolicyCheckDeferredCheck(
                "classification-path",
                $"{classificationPath.DeclaredEntryCount} classification.path declaration(s) require source facts and were not evaluated.",
                classificationPath.PolicyLocations));
        }

        foreach (ArchitectureContractFamilyBinding binding in ArchitectureContractFamilyBindings.All)
        {
            foreach (IArchitectureContract contract in binding.Strict(document.Contracts)
                         .Concat(binding.Audit(document.Contracts)))
            {
                ArchitecturePolicySourceLocation? location = document.Provenance.LocationFor(contract);
                checks.Add(new PolicyCheckDeferredCheck(
                    "contract-evaluation",
                    $"Contract '{contract.Id ?? contract.Name}' requires project, assembly, or source facts and was not evaluated.",
                    location is null ? Array.Empty<ArchitecturePolicySourceLocation>() : [location],
                    binding.FamilyId,
                    contract.Id));
            }
        }

        return checks;
    }
}
