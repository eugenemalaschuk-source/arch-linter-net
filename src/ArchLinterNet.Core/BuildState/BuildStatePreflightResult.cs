using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.BuildState;

public sealed record BuildStatePreflightResult(IReadOnlyList<BuildStatePreflightDiagnostic> Diagnostics)
{
    // Internal handoff to isolated loading: each entry is the receipt digest that was compared
    // with the artifact bytes during this successful preflight. Callers must not recalculate it
    // after Evaluate(), or a replacement in that gap could be accepted as a new baseline.
    internal IReadOnlyDictionary<string, string> VerifiedArtifactContentDigests { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Exact file inputs retained from the preflight's evaluated build manifests. This internal
    // handoff lets the owning analysis snapshot protect inputs after a successful preparation
    // without asking a host to reconstruct source roots or walk the repository again.
    internal IReadOnlyList<string> ConsumedInputPaths { get; init; } = Array.Empty<string>();

    public bool Blocked => Diagnostics.Any(d => d.IsBlocking);
}
