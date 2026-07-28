using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts;

internal static class PolicyCheckSnapshotValidation
{
    public static void Resolve(
        ArchitectureContractDocument document,
        string policyPath,
        IArchitectureFileSystem fileSystem,
        bool wrapUnsafePathFailure)
    {
        if (!wrapUnsafePathFailure)
        {
            PublicApiSnapshotResolver.Resolve(document, policyPath, fileSystem);
            return;
        }

        PublicApiSnapshotResolver.Resolve(document, policyPath, fileSystem, (contract, exception) =>
        {
            ArchitecturePolicySourceLocation? location = document.Provenance.LocationFor(contract);
            if (location is null)
            {
                return;
            }

            var diagnostic = new ArchitecturePolicyDiagnostic(
                ArchitecturePolicyDiagnosticKind.SemanticValidation,
                location,
                Array.Empty<ArchitecturePolicySourceLocation>(),
                location.Source.ImportChain);
            throw new ArchitecturePolicyValidationException(exception.Message, diagnostic, exception);
        });
    }
}
