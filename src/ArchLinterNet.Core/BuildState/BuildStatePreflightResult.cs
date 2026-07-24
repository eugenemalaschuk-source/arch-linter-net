using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.BuildState;

public sealed record BuildStatePreflightResult(IReadOnlyList<BuildStatePreflightDiagnostic> Diagnostics)
{
    public bool Blocked => Diagnostics.Any(d => d.IsBlocking);
}
