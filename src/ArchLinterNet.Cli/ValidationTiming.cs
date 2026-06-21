using System.Diagnostics;

namespace ArchLinterNet.Cli;

internal sealed class ValidationTiming
{
    private readonly List<Entry> _entries = new();
    private int _nextOrdinal;

    public IDisposable Measure(string name, int indent = 0)
    {
        int ordinal = _nextOrdinal++;
        var sw = Stopwatch.StartNew();
        return new PhaseTiming(name, sw, indent, countProvider: null, this, ordinal);
    }

    public IDisposable MeasureContractFamily(string name, Func<int> countProvider, int indent = 1)
    {
        int ordinal = _nextOrdinal++;
        var sw = Stopwatch.StartNew();
        return new PhaseTiming(name, sw, indent, countProvider, this, ordinal);
    }

    public void WriteReport(TextWriter writer)
    {
        Entry? totalEntry = null;
        var phaseEntries = new List<Entry>(_entries.Count);

        foreach (Entry entry in _entries)
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

    internal void Add(string name, long elapsedMs, int indent, int? count, int ordinal)
    {
        _entries.Add(new Entry(name, elapsedMs, indent, count, ordinal));
    }

    private sealed record Entry(string Name, long ElapsedMs, int Indent, int? Count, int Ordinal);

    private sealed class PhaseTiming : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _sw;
        private readonly int _indent;
        private readonly Func<int>? _countProvider;
        private readonly ValidationTiming _owner;
        private readonly int _ordinal;
        private bool _disposed;

        public PhaseTiming(string name, Stopwatch sw, int indent, Func<int>? countProvider,
            ValidationTiming owner, int ordinal)
        {
            _name = name;
            _sw = sw;
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
            _owner.Add(_name, _sw.ElapsedMilliseconds, _indent, count, _ordinal);
        }
    }
}
