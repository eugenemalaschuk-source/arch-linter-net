using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Explicit, hardware-sensitive secondary timing evidence for issue #503. This harness measures
/// full validation over the #502 staged-assembly workloads and applies the reviewed K/P scope model
/// as an expected-effect calculation. It never executes a partial advisory analysis and therefore
/// cannot manufacture evidence that an incremental path is semantically complete.
/// </summary>
[TestFixture]
[Explicit("Hardware-sensitive #503 timing harness — run manually to refresh evidence, never in CI.")]
[Category("Benchmark")]
[CancelAfter(900_000)]
public sealed class ChangedProjectAdvisoryEffectBenchmarkHarness
{
    private const int RunsPerScale = 3;
    private static readonly HashSet<string> _outputPhaseNames = new(StringComparer.Ordinal)
    {
        "render_human", "render_json", "render_sarif", "output_staging", "output_stream_write", "output_commit",
    };
    private static readonly HashSet<string> _fixedPhaseNames = new(StringComparer.Ordinal)
    {
        "policy_composition", "configuration_check", "policy_consistency_check",
    };

    [Test]
    public void RunChangedProjectAdvisoryEffectMatrix()
    {
        Assert.That(File.Exists(CliDllPath()), Is.True,
            $"CLI not built at {CliDllPath()} — run `dotnet build` first.");

        List<ChangedProjectAdvisoryScaleEvidence> scalePoints = new();
        foreach ((string label, int projectCount) in new[] { ("S", 8), ("M", 16), ("L", 32) })
        {
            BenchmarkWorkloadDefinition workload = CreateWorkload(label, projectCount);
            using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
            List<ChangedProjectAdvisoryTimingSample> samples = Enumerable.Range(1, RunsPerScale)
                .Select(sampleOrdinal => RunOnce(fixture, sampleOrdinal))
                .ToList();

            Assert.That(samples.Select(sample => sample.CanonicalResultSha256).Distinct().ToList(), Has.Count.EqualTo(1),
                $"Full strict samples for {label} must preserve canonical result identity.");
            scalePoints.Add(CreateScaleEvidence(workload, samples));
        }

        var document = new ChangedProjectAdvisoryTimingEvidenceDocument
        {
            EvidenceSchemaId = ChangedProjectAdvisoryTimingEvidenceDocument.SchemaId,
            Issue = "#503",
            Outcome = "C",
            SourceIdentity = SourceIdentity(),
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            Configuration = "Debug; staged assemblies; strict; max-parallelism=1",
            MeasurementBoundary = "Full strict analysis-profile/v1 phases only; fixture restore/build and output rendering are excluded. " +
                "The advisory values are modeled from measured fixed/project-dependent phase shares plus measured planner overhead; " +
                "no partial analyzer execution is claimed.",
            DecisionNote = "Outcome C: the timing model shows where K/P could reduce project-dependent work, but ownership " +
                "resolution from evaluated MSBuild Compile items and family-specific correctness remain unsafe for an implementation " +
                "while #991 is open. Full strict validation remains authoritative; no implementation child is justified by this evidence.",
            ScalePoints = scalePoints,
        };

        string resultsPath = ResultsPath();
        File.WriteAllText(resultsPath, ChangedProjectAdvisoryTimingEvidenceJson.Serialize(document));
        TestContext.Out.WriteLine($"Changed-project advisory timing evidence written to {resultsPath}");
        foreach (ChangedProjectAdvisoryScaleEvidence scale in scalePoints)
        {
            TestContext.Out.WriteLine(
                $"{scale.Label} P={scale.ProjectCount}: full={scale.FullValidationMedianMilliseconds:F1}ms " +
                $"fixed={scale.FixedPhaseMedianMilliseconds:F1}ms " +
                $"project-dependent={scale.ProjectDependentPhaseMedianMilliseconds:F1}ms " +
                $"planner={scale.ScopePlanningMedianMilliseconds:F3}ms");
        }
    }

    private static ChangedProjectAdvisoryScaleEvidence CreateScaleEvidence(
        BenchmarkWorkloadDefinition workload,
        IReadOnlyList<ChangedProjectAdvisoryTimingSample> samples)
    {
        decimal fullMilliseconds = Median(samples.Select(FullMilliseconds));
        decimal fixedMilliseconds = Median(samples.Select(FixedMilliseconds));
        decimal projectDependentMilliseconds = fullMilliseconds - fixedMilliseconds;
        decimal planningMilliseconds = MeasurePlanner(workload);
        var estimates = new List<ChangedProjectAdvisoryEstimate>();
        AddEstimate("leaf-project-source-change", ChangedInputKind.ProjectOwnedSourceFile, workload.Projects[0].Id);
        AddEstimate("middle-position-source-change", ChangedInputKind.ProjectOwnedSourceFile,
            workload.Projects[workload.Projects.Count / 2].Id);
        AddEstimate("shared-foundation-project-source-change", ChangedInputKind.ProjectOwnedSourceFile,
            workload.Projects[^1].Id);
        AddEstimate("policy-or-import-change", ChangedInputKind.PolicyOrImportChange);

        return new ChangedProjectAdvisoryScaleEvidence
        {
            Label = $"{workload.Dimensions.ProjectCount} projects",
            ProjectCount = workload.Dimensions.ProjectCount,
            FullProjectCount = workload.Inventory.ProjectCount,
            ScopePlanningSampleCount = 1_000,
            ScopePlanningMedianMilliseconds = planningMilliseconds,
            FullValidationMedianMilliseconds = fullMilliseconds,
            FixedPhaseMedianMilliseconds = fixedMilliseconds,
            ProjectDependentPhaseMedianMilliseconds = projectDependentMilliseconds,
            Estimates = estimates,
            Samples = samples,
        };

        void AddEstimate(string changeClass, ChangedInputKind kind, string? ownerId = null)
        {
            ChangedInput input = new()
            {
                InputId = changeClass,
                Kind = kind,
                OwningProjectIds = ownerId is null ? [] : [ownerId],
            };
            ScopePlan plan = ChangedProjectScopePlanner.Plan(workload.Projects, workload.Edges, [input]);
            decimal ratio = Round((decimal)plan.AffectedProjectCount / workload.Inventory.ProjectCount);
            decimal modeledMilliseconds = Round(fixedMilliseconds + (projectDependentMilliseconds * ratio) + planningMilliseconds);
            decimal reduction = fullMilliseconds > 0
                ? Round(Math.Clamp((1 - (modeledMilliseconds / fullMilliseconds)) * 100, -100, 100))
                : 0;
            estimates.Add(new ChangedProjectAdvisoryEstimate
            {
                ChangeClass = changeClass,
                Disposition = "modeled-only",
                AffectedProjectCount = plan.AffectedProjectCount,
                AffectedScopeRatio = ratio,
                ModeledAdvisoryMilliseconds = modeledMilliseconds,
                ModeledReductionPercent = reduction,
                Authority = kind is ChangedInputKind.PolicyOrImportChange
                    ? "GlobalExpansion/full strict fallback"
                    : "K/P model only; no advisory execution authority",
            });
        }
    }

    private static ChangedProjectAdvisoryTimingSample RunOnce(
        BenchmarkMaterializedFixture fixture,
        int sampleOrdinal)
    {
        string profilePath = Path.Combine(Path.GetTempPath(), $"arch-linter-profile-503-{Guid.NewGuid():N}.json");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = fixture.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.ArgumentList.Add(CliDllPath());
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(fixture.PolicyPath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("strict");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--profile");
        startInfo.ArgumentList.Add(profilePath);
        startInfo.ArgumentList.Add("--max-parallelism");
        startInfo.ArgumentList.Add("1");

        try
        {
            using Process process = Process.Start(startInfo)!;
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();
            Assert.That(File.Exists(profilePath), Is.True,
                $"No profile was written for sample {sampleOrdinal}. stdout:{stdout}{Environment.NewLine}stderr:{stderr}");

            using JsonDocument profileDocument = JsonDocument.Parse(File.ReadAllText(profilePath));
            using JsonDocument resultDocument = JsonDocument.Parse(stdout);
            JsonElement profile = profileDocument.RootElement.Clone();
            JsonElement result = resultDocument.RootElement;
            string canonical = CanonicalResult(result);
            return new ChangedProjectAdvisoryTimingSample
            {
                SampleOrdinal = sampleOrdinal,
                CompletionStatus = profile.GetProperty("CompletionStatus").GetString()!,
                ExitCode = process.ExitCode,
                OutputFailed = profile.GetProperty("Output").GetProperty("OutputFailed").GetBoolean(),
                CanonicalResultSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),
                Counters = ReadCounters(profile),
                TopLevelPhaseMilliseconds = ReadTopLevelPhases(profile),
                RawAnalysisProfile = profile,
            };
        }
        finally
        {
            if (File.Exists(profilePath))
            {
                File.Delete(profilePath);
            }
        }
    }

    private static BenchmarkWorkloadDefinition CreateWorkload(string label, int projectCount) =>
        BenchmarkWorkloadGenerator.Create(
            $"synthetic-changed-project-advisory-{label.ToLowerInvariant()}",
            BenchmarkTopologyShape.Dense,
            new BenchmarkDimensionSet
            {
                ProjectCount = projectCount,
                TypesPerProject = 8,
                SourceFilesPerProject = 2,
                ReferencesPerProject = Math.Min(4, projectCount - 1),
                LayerCount = 4,
                SelectorPredicateTermsPerLayer = 4,
                ContractsPerWorkload = 4,
            },
            BenchmarkCompilationMode.StagedAssemblies,
            BenchmarkExecutionMode.PullRequest);

    private static decimal MeasurePlanner(BenchmarkWorkloadDefinition workload)
    {
        ChangedInput input = new()
        {
            InputId = "planner-timing",
            Kind = ChangedInputKind.ProjectOwnedSourceFile,
            OwningProjectIds = [workload.Projects[0].Id],
        };
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int iteration = 0; iteration < 1_000; iteration++)
        {
            _ = ChangedProjectScopePlanner.Plan(workload.Projects, workload.Edges, [input]);
        }

        stopwatch.Stop();
        return Round((decimal)(stopwatch.Elapsed.TotalMilliseconds / 1_000));
    }

    private static IReadOnlyDictionary<string, int> ReadCounters(JsonElement profile) =>
        profile.GetProperty("Counters").EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out _))
            .ToDictionary(property => property.Name, property => property.Value.GetInt32(), StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, decimal> ReadTopLevelPhases(JsonElement profile) =>
        profile.GetProperty("Phases").EnumerateArray()
            .Where(phase => phase.GetProperty("Indent").GetInt32() == 0 &&
                phase.GetProperty("Name").GetString() != "total" &&
                !_outputPhaseNames.Contains(phase.GetProperty("Name").GetString()!))
            .ToDictionary(
                phase => phase.GetProperty("Name").GetString()!,
                phase => (decimal)phase.GetProperty("ElapsedMs").GetDouble(),
                StringComparer.Ordinal);

    private static decimal FullMilliseconds(ChangedProjectAdvisoryTimingSample sample) =>
        sample.TopLevelPhaseMilliseconds.Values.Sum();

    private static decimal FixedMilliseconds(ChangedProjectAdvisoryTimingSample sample) =>
        sample.TopLevelPhaseMilliseconds
            .Where(phase => _fixedPhaseNames.Contains(phase.Key))
            .Sum(phase => phase.Value);

    private static string CanonicalResult(JsonElement result)
    {
        string[] fields =
        [
            "passed", "mode", "violations", "cycles", "cycle_diagnostics", "coverage_findings",
            "unmatched_ignored_violations", "policy_consistency_findings", "coverage_summary",
            "classification_conflicts", "classification_metadata_failures", "classification_roles",
            "classification_path_deferred", "preflight_diagnostics", "source_set_expansion",
            "subtractive_matcher_participation",
        ];
        return string.Join("\n", fields.Select(field =>
            result.TryGetProperty(field, out JsonElement value) ? value.GetRawText() : "null"));
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        decimal[] sorted = values.Order().ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? Round((sorted[middle - 1] + sorted[middle]) / 2) : Round(sorted[middle]);
    }

    private static decimal Round(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static string CliDllPath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");

    private static string ResultsPath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(), "docs", "internal", "changed-project-advisory-analysis-timing-results.json");

    private static string SourceIdentity()
    {
        string? supplied = Environment.GetEnvironmentVariable("ARCH_LINTER_SOURCE_SHA");
        return string.IsNullOrWhiteSpace(supplied) ? "working-tree" : supplied;
    }
}
