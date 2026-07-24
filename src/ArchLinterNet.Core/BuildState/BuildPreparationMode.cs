namespace ArchLinterNet.Core.BuildState;

public enum BuildPreparationMode
{
    // Ordinary validation: never restores or builds. Missing/stale/mismatched artifacts fail
    // closed with a typed diagnostic.
    Ordinary,

    // Explicit opt-in: builds the graph once via a structured `dotnet build` invocation, emits a
    // build receipt, and re-verifies artifacts from that receipt before validation proceeds.
    EnsureBuilt
}
