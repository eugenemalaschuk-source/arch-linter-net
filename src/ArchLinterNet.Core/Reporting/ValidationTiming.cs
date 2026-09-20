using System.Diagnostics;

namespace ArchLinterNet.Core.Reporting;

public sealed class ValidationTiming
{
    private readonly List<Entry> _entries = new();
    private int _nextOrdinal;
    private long _selectorElapsedStopwatchTicks;
    private int _selectorMeasurementCount;

    public IDisposable Measure(string name, int indent = 0)
    {
        int ordinal = _nextOrdinal++;
        var sw = Stopwatch.StartNew();
        return new PhaseTiming(name, sw, Process.GetCurrentProcess().TotalProcessorTime, indent, countProvider: null, this, ordinal);
    }

    public IDisposable MeasureContractFamily(string name, Func<int> countProvider, int indent = 1)
    {
        int ordinal = _nextOrdinal++;
        var sw = Stopwatch.StartNew();
        return new PhaseTiming(name, sw, Process.GetCurrentProcess().TotalProcessorTime, indent, countProvider, this, ordinal);
    }

    public void WriteReport(TextWriter writer)
    {
        Entry? totalEntry = null;
        IReadOnlyList<Entry> entries = Entries;
        var phaseEntries = new List<Entry>(entries.Count);

        foreach (Entry entry in entries)
        {
            if (entry.Name == "total")
                totalEntry = entry;
            else
                phaseEntries.Add(entry);
        }

        if (totalEntry == null)
            return;

        // Sort by creation ordinal to ensure parent-before-child order
        phaseEntries.Sort((a, b) => a.Ordinal.CompareTo(b.Ordinal));

        int maxLabelWidth = "total".Length;
        var labelCache = new string[phaseEntries.Count];

        for (int i = 0; i < phaseEntries.Count; i++)
        {
            Entry entry = phaseEntries[i];
            string indent = new(' ', entry.Indent * 2);
            string countPart = entry.Count.HasValue ? $" count={entry.Count.Value}" : "";
            string label = $"{indent}{entry.Name}{countPart}";
            if (label.Length > maxLabelWidth)
                maxLabelWidth = label.Length;
            labelCache[i] = label;
        }

        int durationWidth = $"{totalEntry.ElapsedMs} ms".Length;
        foreach (Entry entry in phaseEntries)
        {
            int len = $"{entry.ElapsedMs} ms".Length;
            if (len > durationWidth)
                durationWidth = len;
        }

        writer.WriteLine("Validation timings:");
        writer.WriteLine($"  {"total".PadRight(maxLabelWidth)}  {$"{totalEntry.ElapsedMs} ms".PadLeft(durationWidth)}");
        writer.WriteLine();

        for (int i = 0; i < phaseEntries.Count; i++)
        {
            writer.WriteLine($"  {labelCache[i].PadRight(maxLabelWidth)}  {$"{phaseEntries[i].ElapsedMs} ms".PadLeft(durationWidth)}");
        }
    }

    // Read-only view for AnalysisProfileBuilder (ArchLinterNet.Core.Profiling) to derive deterministic
    // phase/count data without changing WriteReport's own human-text rendering.
    internal IReadOnlyList<Entry> Entries => SnapshotEntries();

    // Selector predicates are counted independently from timing so ordinary validation keeps the
    // low-overhead deterministic counter. When profiling is enabled, callers add high-resolution
    // wall-time samples here and this view exposes one aggregate phase instead of one phase entry
    // per predicate invocation. Process-wide CPU time is intentionally not recorded: it cannot be
    // attributed to an individual predicate evaluation without being distorted by unrelated work
    // or overlapping evaluations.
    internal void RecordSelectorPredicateWallTime(long elapsedStopwatchTicks)
    {
        if (elapsedStopwatchTicks < 0)
        {
            return;
        }

        Interlocked.Add(ref _selectorElapsedStopwatchTicks, elapsedStopwatchTicks);
        Interlocked.Increment(ref _selectorMeasurementCount);
    }

    internal void Add(string name, long elapsedMs, double processorTimeMs, int indent, int? count, int ordinal)
    {
        _entries.Add(new Entry(name, elapsedMs, processorTimeMs, indent, count, ordinal));
    }

    internal sealed record Entry(string Name, long ElapsedMs, double? ProcessorTimeMs, int Indent, int? Count, int Ordinal)
    {
        internal double? HighResolutionElapsedMs { get; init; }
    }

    private IReadOnlyList<Entry> SnapshotEntries()
    {
        int selectorMeasurementCount = Volatile.Read(ref _selectorMeasurementCount);
        if (selectorMeasurementCount == 0)
        {
            return _entries;
        }

        List<Entry> entries = new(_entries);
        double elapsedMs = Volatile.Read(ref _selectorElapsedStopwatchTicks) * 1000d / Stopwatch.Frequency;
        int ordinal = entries.Count == 0 ? 0 : entries.Max(entry => entry.Ordinal) + 1;
        entries.Add(new Entry(
            "selector_predicate_evaluation",
            (long)Math.Round(elapsedMs),
            ProcessorTimeMs: null,
            Indent: 1,
            Count: null,
            Ordinal: ordinal)
        {
            HighResolutionElapsedMs = elapsedMs,
        });
        entries.Sort((left, right) => left.Ordinal.CompareTo(right.Ordinal));
        return entries;
    }

    private sealed class PhaseTiming : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _sw;
        private readonly TimeSpan _processorTimeAtStart;
        private readonly int _indent;
        private readonly Func<int>? _countProvider;
        private readonly ValidationTiming _owner;
        private readonly int _ordinal;
        private bool _disposed;

        public PhaseTiming(string name, Stopwatch sw, TimeSpan processorTimeAtStart, int indent, Func<int>? countProvider,
            ValidationTiming owner, int ordinal)
        {
            _name = name;
            _sw = sw;
            _processorTimeAtStart = processorTimeAtStart;
            _indent = indent;
            _countProvider = countProvider;
            _owner = owner;
            _ordinal = ordinal;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _sw.Stop();
            int? count = _countProvider?.Invoke();
            double processorTimeMs = Math.Max(0, (Process.GetCurrentProcess().TotalProcessorTime - _processorTimeAtStart).TotalMilliseconds);
            _owner.Add(_name, _sw.ElapsedMilliseconds, processorTimeMs, _indent, count, _ordinal);
        }
    }
}
