using System.Diagnostics;
using System.Globalization;

namespace ArchLinterNet.Core.History;

// Optional, human-readable evidence for the history-specific performance acceptance. The timing
// object is deliberately side-band: it never participates in result identity, ordering, scoring,
// or publication decisions.
internal sealed class HistoryIngestionTiming
{
    private readonly Dictionary<string, TimeSpan> _durations = new(StringComparer.Ordinal);

    public int IngestionInvocationCount { get; private set; }

    public void MarkIngestionInvocation() => IngestionInvocationCount++;

    public Stopwatch Start() => Stopwatch.StartNew();

    public void Record(string phase, Stopwatch? stopwatch)
    {
        if (stopwatch is not null)
        {
            _durations[phase] = stopwatch.Elapsed;
        }
    }

    public string Format()
    {
        static string Milliseconds(TimeSpan value) => value.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture);

        string Phase(string name) => _durations.TryGetValue(name, out TimeSpan value) ? Milliseconds(value) : "n/a";
        return "History timings (ms): " +
            $"ingestion={Phase("ingestion")}; " +
            $"scoring={Phase("scoring")}; " +
            $"policy={Phase("policy")}; " +
            $"enrichment={Phase("enrichment")}; " +
            $"json_render={Phase("json_render")}; " +
            $"markdown_render={Phase("markdown_render")}; " +
            $"output={Phase("output")}; " +
            $"ingestion_calls={IngestionInvocationCount.ToString(CultureInfo.InvariantCulture)}";
    }
}
