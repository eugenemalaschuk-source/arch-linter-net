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
        try
        {
            PublicApiSnapshotResolver.Resolve(document, policyPath, fileSystem);
        }
        catch (InvalidOperationException exception) when (wrapUnsafePathFailure)
        {
            ArchitecturePolicySourceLocation? location = document.Contracts.StrictPublicApiSurface
                .Concat(document.Contracts.AuditPublicApiSurface)
                .Select(document.Provenance.LocationFor)
                .FirstOrDefault(candidate => candidate is not null);
            if (location is null)
            {
                throw;
            }

            var diagnostic = new ArchitecturePolicyDiagnostic(
                ArchitecturePolicyDiagnosticKind.SemanticValidation,
                location,
                Array.Empty<ArchitecturePolicySourceLocation>(),
                location.Source.ImportChain);
            throw new ArchitecturePolicyValidationException(exception.Message, diagnostic, exception);
        }
    }
}
