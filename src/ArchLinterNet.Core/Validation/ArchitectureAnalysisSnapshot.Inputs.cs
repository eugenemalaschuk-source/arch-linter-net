using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureAnalysisSnapshot
{
    // These narrow internal accessors let Core-owned review workflows expose the exact input
    // provenance already captured by the snapshot. They intentionally return snapshots of the
    // collections, never the mutable runner/session state.
    internal IReadOnlyList<string> GetCapturePolicyImportPaths() => GetPolicyImportPaths();

    internal IReadOnlyList<string> GetCaptureResolvedAssemblyPaths() => GetResolvedAssemblyPaths();

    internal IReadOnlyList<string> GetCaptureDiscoveredProjectPaths() => GetDiscoveredProjectPaths();

    private List<string> GetPolicyImportPaths()
    {
        return _document.Provenance.Sources
            .Select(source => Path.GetFullPath(Path.Combine(_repositoryRoot, source.SourcePath)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // This is deliberately a snapshot copy, not the internal mutable counter record. Hosts use
    // it when cancellation interrupts evaluation before a ValidationOutcome can expose inputs.
    public IReadOnlyList<string> GetProfileInputPaths() => GetPolicyImportPaths()
        .Concat(GetResolvedAssemblyPaths()
            .SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }))
        .Concat(GetDiscoveredProjectPaths())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
