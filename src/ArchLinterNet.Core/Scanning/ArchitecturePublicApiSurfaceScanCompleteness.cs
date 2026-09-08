namespace ArchLinterNet.Core.Scanning;

// Shared mutable evidence for one exported-surface traversal. The façade and its member
// collaborator intentionally observe the same instance so a reflection failure in either path
// makes the one materialization incomplete without triggering a second scan.
internal sealed class SurfaceScanCompleteness
{
    internal SurfaceScanCompleteness(bool isComplete) => IsComplete = isComplete;

    internal bool IsComplete { get; private set; }

    internal void MarkIncomplete() => IsComplete = false;
}
