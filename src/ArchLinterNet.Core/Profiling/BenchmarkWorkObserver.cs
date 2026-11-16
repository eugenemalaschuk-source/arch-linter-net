using System.Collections.ObjectModel;

namespace ArchLinterNet.Core.Profiling;

/// <summary>
///     Optional, per-invocation observations for a small fixed set of Core work boundaries used by
///     benchmark evidence. Callers own the instance and pass it explicitly; it has no ambient state.
/// </summary>
internal sealed class BenchmarkWorkObserver
{
    private long _astSemanticLookups;
    private long _celContextConstructions;
    private long _layerMembershipProbes;
    private long _methodBodyBuildContextResolutions;
    private long _methodBodyCompilationCalls;
    private long _methodBodyIlMethodVisits;
    private long _methodBodyIlTypeVisits;
    private long _methodBodyOwnershipComparisons;
    private long _methodBodyOwnershipProjectDirectories;
    private long _methodBodyOwnershipLookups;
    private long _methodBodySourceDirectoriesEnumerated;
    private long _methodBodySourceFilesEnumerated;
    private long _methodBodySourceFileReads;
    private long _methodBodySyntaxTreeParseCalls;
    private long _methodBodySyntaxTrees;
    private long _projectFileParses;
    private long _sourceFactFileParses;

    internal long ProjectFileParses => Interlocked.Read(ref _projectFileParses);
    internal long SourceFactFileParses => Interlocked.Read(ref _sourceFactFileParses);
    internal long MethodBodyCompilationCalls => Interlocked.Read(ref _methodBodyCompilationCalls);
    internal long MethodBodySyntaxTrees => Interlocked.Read(ref _methodBodySyntaxTrees);
    internal long MethodBodySyntaxTreeParseCalls => Interlocked.Read(ref _methodBodySyntaxTreeParseCalls);
    internal long MethodBodySourceDirectoriesEnumerated => Interlocked.Read(ref _methodBodySourceDirectoriesEnumerated);
    internal long MethodBodySourceFilesEnumerated => Interlocked.Read(ref _methodBodySourceFilesEnumerated);
    internal long MethodBodySourceFileReads => Interlocked.Read(ref _methodBodySourceFileReads);
    internal long MethodBodyOwnershipLookups => Interlocked.Read(ref _methodBodyOwnershipLookups);
    internal long MethodBodyOwnershipProjectDirectories => Interlocked.Read(ref _methodBodyOwnershipProjectDirectories);
    internal long MethodBodyOwnershipComparisons => Interlocked.Read(ref _methodBodyOwnershipComparisons);
    internal long MethodBodyBuildContextResolutions => Interlocked.Read(ref _methodBodyBuildContextResolutions);
    internal long MethodBodyIlTypeVisits => Interlocked.Read(ref _methodBodyIlTypeVisits);
    internal long MethodBodyIlMethodVisits => Interlocked.Read(ref _methodBodyIlMethodVisits);
    internal long AstSemanticLookups => Interlocked.Read(ref _astSemanticLookups);
    internal long LayerMembershipProbes => Interlocked.Read(ref _layerMembershipProbes);
    internal long CelContextConstructions => Interlocked.Read(ref _celContextConstructions);

    internal void RecordProjectFileParse()
    {
        Interlocked.Increment(ref _projectFileParses);
    }

    internal void RecordSourceFactFileParse()
    {
        Interlocked.Increment(ref _sourceFactFileParses);
    }

    internal void RecordMethodBodyCompilation(long syntaxTreeCount)
    {
        Interlocked.Increment(ref _methodBodyCompilationCalls);
        Interlocked.Add(ref _methodBodySyntaxTrees, syntaxTreeCount);
    }

    internal void RecordMethodBodySyntaxTreeParse()
    {
        Interlocked.Increment(ref _methodBodySyntaxTreeParseCalls);
    }

    internal void RecordMethodBodySourceDirectoryEnumerated()
    {
        Interlocked.Increment(ref _methodBodySourceDirectoriesEnumerated);
    }

    internal void RecordMethodBodySourceFileEnumerated()
    {
        Interlocked.Increment(ref _methodBodySourceFilesEnumerated);
    }

    internal void RecordMethodBodySourceFileRead()
    {
        Interlocked.Increment(ref _methodBodySourceFileReads);
    }

    internal void RecordMethodBodyOwnershipLookup()
    {
        Interlocked.Increment(ref _methodBodyOwnershipLookups);
    }

    internal void RecordMethodBodyOwnershipProjectDirectory()
    {
        Interlocked.Increment(ref _methodBodyOwnershipProjectDirectories);
    }

    internal void RecordMethodBodyOwnershipComparison()
    {
        Interlocked.Increment(ref _methodBodyOwnershipComparisons);
    }

    internal void RecordMethodBodyBuildContextResolution()
    {
        Interlocked.Increment(ref _methodBodyBuildContextResolutions);
    }

    internal void RecordMethodBodyIlTypeVisit()
    {
        Interlocked.Increment(ref _methodBodyIlTypeVisits);
    }

    internal void RecordMethodBodyIlMethodVisit()
    {
        Interlocked.Increment(ref _methodBodyIlMethodVisits);
    }

    internal void RecordAstSemanticLookup()
    {
        Interlocked.Increment(ref _astSemanticLookups);
    }

    internal void RecordLayerMembershipProbe()
    {
        Interlocked.Increment(ref _layerMembershipProbes);
    }

    internal void RecordCelContextConstruction()
    {
        Interlocked.Increment(ref _celContextConstructions);
    }

    /// <summary>
    ///     Returns only boundaries observed at least once. A missing key therefore means that the
    ///     invocation did not exercise that boundary, while the properties above retain exact zero
    ///     values for focused assertions.
    /// </summary>
    internal IReadOnlyDictionary<string, long> Snapshot()
    {
        Dictionary<string, long> values = new(StringComparer.Ordinal);
        AddIfObserved(values, nameof(ProjectFileParses), ProjectFileParses);
        AddIfObserved(values, nameof(SourceFactFileParses), SourceFactFileParses);
        AddIfObserved(values, nameof(MethodBodyCompilationCalls), MethodBodyCompilationCalls);
        AddIfObserved(values, nameof(MethodBodySyntaxTrees), MethodBodySyntaxTrees);
        AddIfObserved(values, nameof(MethodBodySyntaxTreeParseCalls), MethodBodySyntaxTreeParseCalls);
        AddIfObserved(values, nameof(MethodBodySourceDirectoriesEnumerated), MethodBodySourceDirectoriesEnumerated);
        AddIfObserved(values, nameof(MethodBodySourceFilesEnumerated), MethodBodySourceFilesEnumerated);
        AddIfObserved(values, nameof(MethodBodySourceFileReads), MethodBodySourceFileReads);
        AddIfObserved(values, nameof(MethodBodyOwnershipLookups), MethodBodyOwnershipLookups);
        AddIfObserved(values, nameof(MethodBodyOwnershipProjectDirectories), MethodBodyOwnershipProjectDirectories);
        AddIfObserved(values, nameof(MethodBodyOwnershipComparisons), MethodBodyOwnershipComparisons);
        AddIfObserved(values, nameof(MethodBodyBuildContextResolutions), MethodBodyBuildContextResolutions);
        AddIfObserved(values, nameof(MethodBodyIlTypeVisits), MethodBodyIlTypeVisits);
        AddIfObserved(values, nameof(MethodBodyIlMethodVisits), MethodBodyIlMethodVisits);
        AddIfObserved(values, nameof(AstSemanticLookups), AstSemanticLookups);
        AddIfObserved(values, nameof(LayerMembershipProbes), LayerMembershipProbes);
        AddIfObserved(values, nameof(CelContextConstructions), CelContextConstructions);
        return new ReadOnlyDictionary<string, long>(values);
    }

    private static void AddIfObserved(Dictionary<string, long> values, string name, long value)
    {
        if (value > 0) values.Add(name, value);
    }
}
