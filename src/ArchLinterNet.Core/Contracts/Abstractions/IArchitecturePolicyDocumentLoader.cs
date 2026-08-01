using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Abstractions;

public interface IArchitecturePolicyDocumentLoader
{
    ArchitectureContractDocument Load(string policyPath);

    // A separate overload (not an optional parameter on Load(string)) so the single-path
    // contract that PolicyDocumentLoader_PublicContractPreservesSinglePathLoadMethod guards stays
    // exactly one parameter. The default implementation only brackets the whole load — a
    // conforming implementation should override this to thread the token into its own
    // import/schema traversal instead of relying on this coarse before/after check.
    ArchitectureContractDocument Load(string policyPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // This method IS the cancellation-aware overload; the token is already observed via the
        // ThrowIfCancellationRequested calls bracketing this call, not by forwarding it further.
        ArchitectureContractDocument document = Load(policyPath); // NOSONAR: see comment above
        cancellationToken.ThrowIfCancellationRequested();
        return document;
    }
}
