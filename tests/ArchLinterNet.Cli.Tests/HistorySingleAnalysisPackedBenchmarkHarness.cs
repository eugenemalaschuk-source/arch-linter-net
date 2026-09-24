using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// Issue #1016: reproducible evidence from the freshly packed and locally installed CLI tool.
// This is deliberately explicit because process timings and working-set readings are runner
// evidence, not a normal correctness gate. Running it packs/installs the current source first.
[TestFixture]
[Explicit("Hardware-sensitive #1016 packed history before/after evidence; run manually to refresh evidence.")]
[Category("Benchmark")]
[CancelAfter(900_000)]
public sealed partial class HistorySingleAnalysisPackedBenchmarkHarness
{
    private const int SamplesPerShape = 3;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [Test]
    public void RunPackedBeforeAfterEvidence()
    {
        string repositoryRoot = FindRepositoryRoot();
        using Fixture fixture = Fixture.Create();
        string cliPath = fixture.PackAndInstall();
        // Warm the host once so first-use framework startup is not mistaken for the shape under
        // test. It is not included in either sample series.
        _ = RunCli(cliPath, fixture.RepositoryPath, fixture.From, fixture.To, ["--format", "json", "--timings"]);

        List<ProcessSample> before = [];
        List<ProcessSample> after = [];
        for (int index = 0; index < SamplesPerShape; index++)
        {
            ProcessSample json = RunCli(
                cliPath, fixture.RepositoryPath, fixture.From, fixture.To,
                ["--format", "json", "--timings"]);
            ProcessSample markdown = RunCli(
                cliPath, fixture.RepositoryPath, fixture.From, fixture.To,
                ["--format", "markdown", "--timings"]);
            AssertSuccess(json, $"before JSON sample {index + 1}");
            AssertSuccess(markdown, $"before Markdown sample {index + 1}");

            string jsonPath = Path.Combine(fixture.OutputDirectory, $"packed-{index + 1}.json");
            string markdownPath = Path.Combine(fixture.OutputDirectory, $"packed-{index + 1}.md");
            ProcessSample packed = RunCli(
                cliPath, fixture.RepositoryPath, fixture.From, fixture.To,
                [
                    "--report", $"json={jsonPath}",
                    "--report", $"markdown={markdownPath}",
                    "--timings",
            ]);
            AssertSuccess(packed, $"packed sample {index + 1}");
            Assert.That(packed.StdOut, Is.Empty, "File sinks must not contaminate machine stdout.");
            AssertUtf8BytesEqual(jsonPath, json.StdOut, "Packed JSON changed bytes.");
            AssertUtf8BytesEqual(markdownPath, markdown.StdOut, "Packed Markdown changed bytes.");

            before.Add(new ProcessSample
            {
                Shape = "before-two-processes",
                Format = "json+markdown",
                WallClockMilliseconds = json.WallClockMilliseconds + markdown.WallClockMilliseconds,
                PeakWorkingSetBytes = Math.Max(json.PeakWorkingSetBytes, markdown.PeakWorkingSetBytes),
                Timing = AddTimings(json.Timing, markdown.Timing),
                ProcessOverheadMilliseconds = json.ProcessOverheadMilliseconds + markdown.ProcessOverheadMilliseconds,
                InvocationCount = json.InvocationCount + markdown.InvocationCount,
            });
            after.Add(packed with { Shape = "after-one-process", Format = "json+markdown" });
        }

        EvidenceDocument evidence = BuildEvidence(repositoryRoot, cliPath, fixture, before, after);
        string evidencePath = Path.Combine(repositoryRoot, "docs", "internal", "history-single-analysis-packed-evidence.json");
        string markdownEvidencePath = Path.Combine(repositoryRoot, "docs", "internal", "history-single-analysis-packed-evidence.md");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, _jsonOptions) + "\n", new UTF8Encoding(false));
        File.WriteAllText(
            markdownEvidencePath,
            RenderMarkdown(evidence).Replace(Environment.NewLine, "\n", StringComparison.Ordinal),
            new UTF8Encoding(false));
        TestContext.Out.WriteLine($"History packed evidence written to {evidencePath}");
    }

    private static EvidenceDocument BuildEvidence(
        string repositoryRoot,
        string cliPath,
        Fixture fixture,
        IReadOnlyList<ProcessSample> before,
        IReadOnlyList<ProcessSample> after)
    {
        string sourceRevision = Git(repositoryRoot, "rev-parse", "HEAD").Trim();
        if (!string.IsNullOrWhiteSpace(Git(repositoryRoot, "status", "--porcelain", "--untracked-files=all").Trim()))
        {
            sourceRevision += "+dirty";
        }
        return new EvidenceDocument
        {
            SchemaId = "history-single-analysis-packed-evidence/v1",
            Issue = "#1016",
            SourceRevision = sourceRevision,
            ToolPath = Path.GetFileName(cliPath),
            ToolVersion = Fixture.PackedToolVersion,
            ToolPackageSha256 = fixture.PackageSha256,
            Runtime = RuntimeInformation.FrameworkDescription,
            SdkVersion = fixture.SdkVersion,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Configuration = "Release packed NuGet tool",
            Fixture = new FixtureEvidence
            {
                Kind = "synthetic-git-explicit-release-range",
                From = fixture.From,
                To = fixture.To,
                CommitCount = fixture.CommitCount,
                Policy = "default history_analysis configuration",
            },
            Before = Summarize(before),
            After = Summarize(after),
            Notes =
            [
                "Before is two independent invocations of the same freshly packed and locally installed CLI package (one JSON and one Markdown); after is one invocation with two file sinks.",
                "The package SHA-256 identifies the exact packed tool used by every before/after sample.",
                "Phase timings come from --timings. process_overhead_ms is wall clock minus reported policy/ingestion/scoring/render/output phases.",
                "Measurements are runner evidence, not a promise that a complete release job is exactly twice as fast.",
            ],
        };
    }

    private static ShapeSummary Summarize(IReadOnlyList<ProcessSample> samples)
    {
        return new ShapeSummary
        {
            Samples = samples,
            AverageWallClockMilliseconds = samples.Average(sample => sample.WallClockMilliseconds),
            AverageIngestionMilliseconds = AveragePhase(samples, "ingestion"),
            AverageScoringMilliseconds = AveragePhase(samples, "scoring"),
            AverageJsonRenderingMilliseconds = AveragePhase(samples, "json_render"),
            AverageMarkdownRenderingMilliseconds = AveragePhase(samples, "markdown_render"),
            AverageProcessOverheadMilliseconds = samples.Average(sample => sample.ProcessOverheadMilliseconds),
            PeakWorkingSetBytes = samples.Max(sample => sample.PeakWorkingSetBytes),
            IngestionInvocationCounts = samples.Select(sample => sample.InvocationCount).ToArray(),
        };
    }

    private static double AveragePhase(IReadOnlyList<ProcessSample> samples, string name) =>
        samples.Average(sample => sample.Timing.TryGetValue(name, out double value) ? value : 0d);

    private static Dictionary<string, double> AddTimings(IReadOnlyDictionary<string, double> first, IReadOnlyDictionary<string, double> second)
    {
        Dictionary<string, double> result = new(StringComparer.Ordinal);
        foreach (string key in first.Keys.Concat(second.Keys).Distinct(StringComparer.Ordinal))
        {
            result[key] = first.GetValueOrDefault(key) + second.GetValueOrDefault(key);
        }

        return result;
    }

    private static ProcessSample RunCli(
        string cliPath,
        string repositoryPath,
        string from,
        string to,
        IReadOnlyList<string> arguments)
    {
        ProcessStartInfo startInfo = new(cliPath)
        {
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("history");
        startInfo.ArgumentList.Add("analyze");
        startInfo.ArgumentList.Add("--repository");
        startInfo.ArgumentList.Add(repositoryPath);
        startInfo.ArgumentList.Add("--from");
        startInfo.ArgumentList.Add(from);
        startInfo.ArgumentList.Add("--to");
        startInfo.ArgumentList.Add(to);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Stopwatch wallClock = Stopwatch.StartNew();
        using Process process = Process.Start(startInfo)!;
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        long peakWorkingSetBytes = 0;
        while (!process.HasExited)
        {
            try
            {
                process.Refresh();
                peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, process.WorkingSet64);
            }
            catch (InvalidOperationException)
            {
                break;
            }

            Thread.Sleep(1);
        }
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        wallClock.Stop();

        Dictionary<string, double> timings = ParseTimings(stderrTask.Result);
        int invocationCount = ParseInvocationCount(stderrTask.Result);
        double reportedPhaseMilliseconds = timings.Values.Sum();
        return new ProcessSample
        {
            ExitCode = process.ExitCode,
            WallClockMilliseconds = wallClock.Elapsed.TotalMilliseconds,
            PeakWorkingSetBytes = peakWorkingSetBytes,
            StdOut = stdoutTask.Result,
            StdErr = stderrTask.Result,
            Timing = timings,
            InvocationCount = invocationCount,
            ProcessOverheadMilliseconds = Math.Max(0d, wallClock.Elapsed.TotalMilliseconds - reportedPhaseMilliseconds),
        };
    }

    private static Dictionary<string, double> ParseTimings(string stderr)
    {
        string line = stderr.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Single(value => value.StartsWith("History timings (ms):", StringComparison.Ordinal));
        Dictionary<string, double> timings = new(StringComparer.Ordinal);
        foreach (string part in line["History timings (ms):".Length..].Split(';', StringSplitOptions.TrimEntries))
        {
            string[] pair = part.Split('=', 2);
            if (pair.Length == 2 && double.TryParse(pair[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value))
            {
                if (!string.Equals(pair[0], "ingestion_calls", StringComparison.Ordinal))
                {
                    timings[pair[0]] = value;
                }
            }
        }

        return timings;
    }

    private static int ParseInvocationCount(string stderr)
    {
        string line = stderr.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Single(value => value.StartsWith("History timings (ms):", StringComparison.Ordinal));
        string value = line.Split(';', StringSplitOptions.TrimEntries)
            .Single(part => part.StartsWith("ingestion_calls=", StringComparison.Ordinal))
            .Split('=', 2)[1];
        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AssertSuccess(ProcessSample sample, string name)
    {
        Assert.That(sample.ExitCode, Is.EqualTo(0), $"{name} failed: {sample.StdErr}");
        Assert.That(sample.InvocationCount, Is.EqualTo(1), $"{name} must perform one ingestion.");
    }

    private static void AssertUtf8BytesEqual(string path, string expectedText, string message)
    {
        byte[] actual = File.ReadAllBytes(path);
        byte[] expected = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expectedText);
        Assert.That(actual, Is.EqualTo(expected), message);
    }

    private static string RenderMarkdown(EvidenceDocument evidence)
    {
        static string F(double value) => value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        StringBuilder text = new();
        text.AppendLine("# History single-analysis packed evidence");
        text.AppendLine();
        text.AppendLine($"- Issue: `{evidence.Issue}`");
        text.AppendLine($"- Source revision: `{evidence.SourceRevision}`");
        text.AppendLine($"- Tool package SHA-256: `{evidence.ToolPackageSha256}`");
        text.AppendLine($"- Fixture range: `{evidence.Fixture.From}` → `{evidence.Fixture.To}` ({evidence.Fixture.CommitCount} commits)");
        text.AppendLine($"- SDK/runtime: {evidence.SdkVersion} / {evidence.Runtime}; {evidence.OperatingSystem}; {evidence.Architecture}");
        text.AppendLine();
        text.AppendLine("Reproduce from a restored checkout with:");
        text.AppendLine();
        text.AppendLine("```text");
        text.AppendLine("dotnet test tests/ArchLinterNet.Cli.Tests --no-restore --filter FullyQualifiedName~HistorySingleAnalysisPackedBenchmarkHarness -- NUnit.ExplicitTests=true");
        text.AppendLine("```");
        text.AppendLine();
        text.AppendLine("| Shape | Wall ms | Ingestion ms | Scoring ms | JSON ms | Markdown ms | Process overhead ms | Peak working set | Ingestion calls |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        text.AppendLine($"| Before: two processes | {F(evidence.Before.AverageWallClockMilliseconds)} | {F(evidence.Before.AverageIngestionMilliseconds)} | {F(evidence.Before.AverageScoringMilliseconds)} | {F(evidence.Before.AverageJsonRenderingMilliseconds)} | {F(evidence.Before.AverageMarkdownRenderingMilliseconds)} | {F(evidence.Before.AverageProcessOverheadMilliseconds)} | {evidence.Before.PeakWorkingSetBytes} | `{string.Join(",", evidence.Before.IngestionInvocationCounts)}` |");
        text.AppendLine($"| After: one packed process | {F(evidence.After.AverageWallClockMilliseconds)} | {F(evidence.After.AverageIngestionMilliseconds)} | {F(evidence.After.AverageScoringMilliseconds)} | {F(evidence.After.AverageJsonRenderingMilliseconds)} | {F(evidence.After.AverageMarkdownRenderingMilliseconds)} | {F(evidence.After.AverageProcessOverheadMilliseconds)} | {evidence.After.PeakWorkingSetBytes} | `{string.Join(",", evidence.After.IngestionInvocationCounts)}` |");
        text.AppendLine();
        foreach (string note in evidence.Notes)
        {
            text.AppendLine($"- {note}");
        }

        return text.ToString();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchLinterNet.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }

    private static string Git(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        }

        return output;
    }

    private sealed record ProcessSample
    {
        public string Shape { get; init; } = string.Empty;
        public string Format { get; init; } = string.Empty;
        public int ExitCode { get; init; }
        public string StdOut { get; init; } = string.Empty;
        public string StdErr { get; init; } = string.Empty;
        public double WallClockMilliseconds { get; init; }
        public long PeakWorkingSetBytes { get; init; }
        public double ProcessOverheadMilliseconds { get; init; }
        public int InvocationCount { get; init; }
        public IReadOnlyDictionary<string, double> Timing { get; init; } = new Dictionary<string, double>();
    }

    private sealed record EvidenceDocument
    {
        public string SchemaId { get; init; } = string.Empty;
        public string Issue { get; init; } = string.Empty;
        public string SourceRevision { get; init; } = string.Empty;
        public string ToolPath { get; init; } = string.Empty;
        public string ToolVersion { get; init; } = string.Empty;
        public string ToolPackageSha256 { get; init; } = string.Empty;
        public string SdkVersion { get; init; } = string.Empty;
        public string Runtime { get; init; } = string.Empty;
        public string OperatingSystem { get; init; } = string.Empty;
        public string Architecture { get; init; } = string.Empty;
        public string Configuration { get; init; } = string.Empty;
        public FixtureEvidence Fixture { get; init; } = new();
        public ShapeSummary Before { get; init; } = new();
        public ShapeSummary After { get; init; } = new();
        public IReadOnlyList<string> Notes { get; init; } = [];
    }

    private sealed record FixtureEvidence
    {
        public string Kind { get; init; } = string.Empty;
        public string From { get; init; } = string.Empty;
        public string To { get; init; } = string.Empty;
        public int CommitCount { get; init; }
        public string Policy { get; init; } = string.Empty;
    }

    private sealed record ShapeSummary
    {
        public IReadOnlyList<ProcessSample> Samples { get; init; } = [];
        public double AverageWallClockMilliseconds { get; init; }
        public double AverageIngestionMilliseconds { get; init; }
        public double AverageScoringMilliseconds { get; init; }
        public double AverageJsonRenderingMilliseconds { get; init; }
        public double AverageMarkdownRenderingMilliseconds { get; init; }
        public double AverageProcessOverheadMilliseconds { get; init; }
        public long PeakWorkingSetBytes { get; init; }
        public IReadOnlyList<int> IngestionInvocationCounts { get; init; } = [];
    }
}
