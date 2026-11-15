using System.Collections.ObjectModel;
using System.Threading;

namespace ArchLinterNet.Core.Profiling;

/// <summary>
/// Optional, per-invocation observations for a small fixed set of Core work boundaries used by
/// benchmark evidence. Callers own the instance and pass it explicitly; it has no ambient state.
/// </summary>
internal sealed class BenchmarkWorkObserver
{
    private long _projectFileParses;
    private long _sourceFactFileParses;
    private long _methodBodyCompilationCalls;
    private long _methodBodySyntaxTrees;
    private long _astSemanticLookups;
    private long _layerMembershipProbes;
    private long _celContextConstructions;

    internal long ProjectFileParses => Interlocked.Read(ref _projectFileParses);
    internal long SourceFactFileParses => Interlocked.Read(ref _sourceFactFileParses);
    internal long MethodBodyCompilationCalls => Interlocked.Read(ref _methodBodyCompilationCalls);
    internal long MethodBodySyntaxTrees => Interlocked.Read(ref _methodBodySyntaxTrees);
    internal long AstSemanticLookups => Interlocked.Read(ref _astSemanticLookups);
    internal long LayerMembershipProbes => Interlocked.Read(ref _layerMembershipProbes);
    internal long CelContextConstructions => Interlocked.Read(ref _celContextConstructions);

    internal void RecordProjectFileParse() => Interlocked.Increment(ref _projectFileParses);
    internal void RecordSourceFactFileParse() => Interlocked.Increment(ref _sourceFactFileParses);
    internal void RecordMethodBodyCompilation(long syntaxTreeCount)
    {
        Interlocked.Increment(ref _methodBodyCompilationCalls);
        Interlocked.Add(ref _methodBodySyntaxTrees, syntaxTreeCount);
    }

    internal void RecordAstSemanticLookup() => Interlocked.Increment(ref _astSemanticLookups);
    internal void RecordLayerMembershipProbe() => Interlocked.Increment(ref _layerMembershipProbes);
    internal void RecordCelContextConstruction() => Interlocked.Increment(ref _celContextConstructions);

    /// <summary>
    /// Returns only boundaries observed at least once. A missing key therefore means that the
    /// invocation did not exercise that boundary, while the properties above retain exact zero
    /// values for focused assertions.
    /// </summary>
    internal IReadOnlyDictionary<string, long> Snapshot()
    {
        Dictionary<string, long> values = new(StringComparer.Ordinal);
        AddIfObserved(values, nameof(ProjectFileParses), ProjectFileParses);
        AddIfObserved(values, nameof(SourceFactFileParses), SourceFactFileParses);
        AddIfObserved(values, nameof(MethodBodyCompilationCalls), MethodBodyCompilationCalls);
        AddIfObserved(values, nameof(MethodBodySyntaxTrees), MethodBodySyntaxTrees);
        AddIfObserved(values, nameof(AstSemanticLookups), AstSemanticLookups);
        AddIfObserved(values, nameof(LayerMembershipProbes), LayerMembershipProbes);
        AddIfObserved(values, nameof(CelContextConstructions), CelContextConstructions);
        return new ReadOnlyDictionary<string, long>(values);
    }

    private static void AddIfObserved(Dictionary<string, long> values, string name, long value)
    {
        if (value > 0)
        {
            values.Add(name, value);
        }
    }
}
