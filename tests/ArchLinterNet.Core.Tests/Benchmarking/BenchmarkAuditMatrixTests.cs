using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Resolution;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Benchmarking;

[TestFixture]
[NonParallelizable]
public sealed class BenchmarkAuditMatrixTests
{
    private const int WarmObserverPairsPerParallelMode = 2;

    [Test]
    [Explicit("Runs the environment-sensitive #1123 repeated baseline matrix and writes issue-scoped evidence.")]
    [CancelAfter(900_000)]
    public void WriteRepeatedBaselineMatrix()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string catalogPath = Path.Combine(
            repositoryRoot,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "workload-catalog.json");
        using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
        string sourceRevision = GetSourceRevision(repositoryRoot);
        string evidenceDirectory = Path.Combine(
            repositoryRoot,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "evidence",
            "issue-1123",
            sourceRevision[..12]);
        Directory.CreateDirectory(evidenceDirectory);

        JsonSchema schema = JsonSchemaTextFactory.FromText(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "benchmark-evidence-v1.schema.json")));
        JsonSchema archiveSchema = JsonSchemaTextFactory.FromText(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "benchmark-evidence-archive-v1.schema.json")));
        int workloadCount = 0;
        foreach (BenchmarkWorkloadDefinition workload in EnumerateAuditScaleWorkloads(catalog.RootElement)
                     .Concat(EnumerateContractGraphScaleWorkloads(catalog.RootElement)))
        {
            string outputPath = Path.Combine(evidenceDirectory, $"{workload.WorkloadId}.json");
            BenchmarkEvidenceDocument evidence = RunWorkload(workload, sourceRevision);
            string evidenceJson = BenchmarkEvidenceJson.Serialize(evidence);
            EvaluationResults validation = schema.Evaluate(
                JsonNode.Parse(evidenceJson), new EvaluationOptions { OutputFormat = OutputFormat.List });
            Assert.That(validation.IsValid, Is.True,
                $"{workload.WorkloadId}: {Describe(validation)}");

            string archivedJson = BenchmarkEvidenceJson.SerializeArchive(evidenceJson);
            string[] evidenceLines = archivedJson.Split(Environment.NewLine);
            int maximumLineBytes = evidenceLines.Max(line => Encoding.UTF8.GetByteCount(line));
            int evidenceBytes = Encoding.UTF8.GetByteCount(archivedJson);
            Assert.That(evidenceLines.Length, Is.LessThan(300),
                $"{workload.WorkloadId} must stay below GitHub's complete-patch review limit.");
            Assert.That(maximumLineBytes, Is.LessThanOrEqualTo(8 * 1024 + 64),
                $"{workload.WorkloadId} contains an overlong JSON line.");
            Assert.That(evidenceBytes, Is.LessThan(96 * 1024),
                $"{workload.WorkloadId} must stay below GitHub's per-file patch limit.");
            EvaluationResults archiveValidation = archiveSchema.Evaluate(
                JsonNode.Parse(archivedJson), new EvaluationOptions { OutputFormat = OutputFormat.List });
            Assert.That(archiveValidation.IsValid, Is.True,
                $"{workload.WorkloadId} archive: {Describe(archiveValidation)}");
            BenchmarkEvidenceDocument restored = BenchmarkEvidenceJson.Deserialize(archivedJson);
            Assert.That(restored.Environment.SourceRevision, Is.EqualTo(sourceRevision));
            File.WriteAllText(outputPath, archivedJson + Environment.NewLine);
            TestContext.Progress.WriteLine(
                $"{workload.WorkloadId}: input={evidence.Environment.MaterializedInputSha256}, result={evidence.CanonicalResult.Sha256}");
            workloadCount++;
        }

        Assert.That(workloadCount, Is.EqualTo(33),
            "The evidence matrix must contain eight independent audit families and three graph shapes, each at three sizes.");
    }

    private static IEnumerable<BenchmarkWorkloadDefinition> EnumerateAuditScaleWorkloads(JsonElement catalog)
    {
        foreach (JsonElement family in catalog.GetProperty("audit_scale_families").EnumerateArray())
        {
            string dimensionName = family.GetProperty("dimension").GetString()!;
            BenchmarkDimensionSet fixedDimensions = BenchmarkJson.Deserialize<BenchmarkDimensionSet>(
                family.GetProperty("fixed_dimensions").GetRawText());
            foreach (JsonElement workloadElement in family.GetProperty("workloads").EnumerateArray())
            {
                string workloadId = workloadElement.GetProperty("id").GetString()!;
                int scale = workloadElement.GetProperty("value").GetInt32();
                BenchmarkDimensionSet dimensions = SetDimension(fixedDimensions, dimensionName, scale);
                yield return BenchmarkWorkloadGenerator.Create(
                    workloadId,
                    BenchmarkTopologyShape.Linear,
                    dimensions);
            }
        }
    }

    private static IEnumerable<BenchmarkWorkloadDefinition> EnumerateContractGraphScaleWorkloads(JsonElement catalog)
    {
        foreach (JsonElement family in catalog.GetProperty("contract_layer_graph_scale_families").EnumerateArray())
        {
            BenchmarkContractLayerGraphShape shape = family.GetProperty("graph_shape").GetString() switch
            {
                "dense_dag" => BenchmarkContractLayerGraphShape.DenseDag,
                "layered_dag" => BenchmarkContractLayerGraphShape.LayeredDag,
                "strongly_connected_with_acyclic_tail" => BenchmarkContractLayerGraphShape
                    .StronglyConnectedWithAcyclicTail,
                not null => throw new InvalidOperationException(
                    $"Unknown graph workload shape '{family.GetProperty("graph_shape").GetString()}'."),
                null => throw new InvalidOperationException("A graph workload shape is required.")
            };
            foreach (JsonElement workloadElement in family.GetProperty("workloads").EnumerateArray())
            {
                string workloadId = workloadElement.GetProperty("id").GetString()!;
                int layerCount = workloadElement.GetProperty("value").GetInt32();
                yield return BenchmarkWorkloadGenerator.Create(
                    workloadId,
                    BenchmarkTopologyShape.Linear,
                    new BenchmarkDimensionSet
                    {
                        ProjectCount = 2,
                        TypesPerProject = 4,
                        SourceFilesPerProject = 2,
                        ReferencesPerProject = 0,
                        LayerCount = layerCount,
                        ContractsPerWorkload = 1,
                        MethodBodyRuleCount = 1
                    },
                    contractLayerGraphShape: shape);
            }
        }
    }

    private static BenchmarkDimensionSet SetDimension(
        BenchmarkDimensionSet dimensions,
        string name,
        int value)
    {
        return name switch
        {
            "project_count" => dimensions with { ProjectCount = value },
            "source_files_per_project" => dimensions with { SourceFilesPerProject = value },
            "contracts_per_workload" => dimensions with { ContractsPerWorkload = value },
            "ignore_rule_count" => dimensions with { IgnoreRuleCount = value },
            "finding_candidates" => dimensions with { FindingCandidates = value },
            "metric_rule_count" => dimensions with { MetricRuleCount = value },
            "report_rule_count" => dimensions with { ReportRuleCount = value },
            "method_body_rule_count" => dimensions with { MethodBodyRuleCount = value },
            _ => throw new InvalidOperationException($"Unknown audit scale dimension '{name}'.")
        };
    }

    private static BenchmarkEvidenceDocument RunWorkload(
        BenchmarkWorkloadDefinition workload,
        string sourceRevision)
    {
        var materialization = Stopwatch.StartNew();
        using BenchmarkMaterializedFixture fixture = BenchmarkFixtureMaterializer.Materialize(workload);
        materialization.Stop();
        BenchmarkResourceMeasurement materializationMeasurement = Milliseconds(materialization.Elapsed);

        var restoreAndBuild = Stopwatch.StartNew();
        fixture.Build();
        restoreAndBuild.Stop();
        BenchmarkResourceMeasurement restoreAndBuildMeasurement = Milliseconds(restoreAndBuild.Elapsed);

        // The direct Core path cannot start a fresh analyzer process. Run one unretained pair to
        // warm JIT and loader paths, then retain repeated warm pairs in both supported parallel modes.
        BenchmarkAuditAnalysisRun warmupDisabled = RunAnalysis(
            fixture, false, 1, "sequential", 1, materializationMeasurement, restoreAndBuildMeasurement);
        BenchmarkAuditAnalysisRun warmupEnabled = RunAnalysis(
            fixture, true, 2, "sequential", 1, materializationMeasurement, restoreAndBuildMeasurement);
        Assert.That(warmupEnabled.CanonicalResult.Sha256, Is.EqualTo(warmupDisabled.CanonicalResult.Sha256));

        List<BenchmarkProfileSample> samples = [];
        BenchmarkCanonicalResultIdentity? canonicalResult = null;
        int ordinal = 1;
        int boundedParallelism = Math.Max(2, Math.Min(8, Environment.ProcessorCount));
        foreach ((string mode, int? parallelism) in new (string Mode, int? Parallelism)[]
                 {
                     ("sequential", 1),
                     ($"bounded-{boundedParallelism}", boundedParallelism)
                 })
            for (int pair = 0; pair < WarmObserverPairsPerParallelMode; pair++)
            {
                bool observerEnabledFirst = pair % 2 == 1;
                BenchmarkAuditAnalysisRun first = RunAnalysis(
                    fixture, observerEnabledFirst, ordinal++, mode, parallelism,
                    materializationMeasurement, restoreAndBuildMeasurement);
                BenchmarkAuditAnalysisRun second = RunAnalysis(
                    fixture, !observerEnabledFirst, ordinal++, mode, parallelism,
                    materializationMeasurement, restoreAndBuildMeasurement);
                BenchmarkAuditAnalysisRun disabled = observerEnabledFirst ? second : first;
                BenchmarkAuditAnalysisRun enabled = observerEnabledFirst ? first : second;

                Assert.That(enabled.CanonicalResult.Sha256, Is.EqualTo(disabled.CanonicalResult.Sha256),
                    $"Observer parity failed for {workload.WorkloadId} ({mode}, pair {pair + 1}).");
                Assert.That(enabled.MaterializedInputSha256, Is.EqualTo(disabled.MaterializedInputSha256));
                Assert.That(enabled.Sample.LoadedAssemblies!.Sha256,
                    Is.EqualTo(disabled.Sample.LoadedAssemblies!.Sha256),
                    $"Loaded assemblies changed across paired samples for {workload.WorkloadId} ({mode}, pair {pair + 1}).");
                canonicalResult ??= disabled.CanonicalResult;
                Assert.That(disabled.CanonicalResult.Sha256, Is.EqualTo(canonicalResult.Sha256),
                    $"Canonical result changed across samples for {workload.WorkloadId}.");

                BenchmarkProfilerOverheadEvidence overhead =
                    BenchmarkAuditAnalysisRunner.CreateObserverOverhead(disabled, enabled);
                samples.Add(AddReportMeasurement(fixture, first.Sample, overhead));
                samples.Add(AddReportMeasurement(fixture, second.Sample, overhead));
            }

        Assert.That(samples, Has.Count.EqualTo(8), $"Unexpected sample count for {workload.WorkloadId}.");
        Assert.That(samples.All(sample => sample.LoadedAssemblies is { Artifacts.Count: > 0 }), Is.True,
            $"Loaded DLL identities were missing for {workload.WorkloadId}.");
        Assert.That(samples.Select(sample => sample.LoadedAssemblies!.Sha256)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(1),
            $"Loaded DLL identity changed across repeated samples for {workload.WorkloadId}.");
        if (workload.Dimensions.MetricRuleCount > 0)
        {
            Assert.That(samples.All(sample => sample.MetricWork?.MetricCount == workload.Dimensions.MetricRuleCount),
                Is.True,
                $"Metric execution count did not match the declared scale for {workload.WorkloadId}.");
            Assert.That(samples.Select(sample => sample.MetricWork!.ResultsSha256)
                    .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(1),
                $"Metric result identity changed across samples for {workload.WorkloadId}.");
        }

        if (workload.Dimensions.MethodBodyRuleCount > 0)
            foreach (BenchmarkProfileSample sample in samples)
            {
                JsonElement contractCounts = sample.RawAnalysisProfile
                    .GetProperty("Counters")
                    .GetProperty("ContractFamilyCounts");
                Assert.That(contractCounts.GetProperty("dependency").GetInt32(),
                    Is.EqualTo(workload.Dimensions.ContractsPerWorkload),
                    $"Ordinary contracts changed with method-body scale for {workload.WorkloadId}.");
                Assert.That(contractCounts.GetProperty("method_body").GetInt32(),
                    Is.EqualTo(workload.Dimensions.MethodBodyRuleCount),
                    $"Method-body work did not match the declared scale for {workload.WorkloadId}.");
            }

        string[] reportChecksums = samples
            .Where(sample => sample.ReportOutputSha256 is not null)
            .Select(sample => sample.ReportOutputSha256!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.That(reportChecksums, Has.Length.LessThanOrEqualTo(1),
            $"Report output changed across repeated samples for {workload.WorkloadId}.");

        string inputIdentity = BenchmarkMaterializedInputIdentity.Compute(fixture.Root);
        using var profile = JsonDocument.Parse(samples[0].RawAnalysisProfile.GetRawText());
        string configuration = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "unknown";
        BenchmarkEvidenceDocument template = BenchmarkEvidenceFactory.Create(
            workload,
            profile.RootElement.Clone(),
            canonicalResult!,
            samples[0]);
        BenchmarkEvidenceDocument evidence = template with
        {
            Samples = samples,
            Environment = new BenchmarkEnvironmentEvidence
            {
                Runtime = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Configuration = $"{configuration}; in-process Core; no analyzer child process",
                SourceIdentity = workload.WorkloadIdentity,
                SourceRevision = sourceRevision,
                RunnerIdentity =
                    $"local-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}-{Environment.ProcessorCount}cpu",
                ToolIdentity = $"ArchLinterNet.Core.Tests/{Assembly.GetExecutingAssembly().GetName().Version}",
                MaterializedInputSha256 = inputIdentity,
                PeakWorkingSet = BenchmarkResourceMeasurement.Unavailable(
                    "A process-wide peak working set would include the complete NUnit host lifetime and is not attributable to this workload."),
                ManagedAllocation = BenchmarkResourceMeasurement.Unavailable(
                    "The evidence retains each isolated Core analysis allocation sample rather than aggregating unrelated test-host allocations.")
            }
        };
        evidence.Validate();
        return evidence;
    }

    private static BenchmarkAuditAnalysisRun RunAnalysis(
        BenchmarkMaterializedFixture fixture,
        bool observerEnabled,
        int ordinal,
        string mode,
        int? parallelism,
        BenchmarkResourceMeasurement materialization,
        BenchmarkResourceMeasurement restoreAndBuild)
    {
        return BenchmarkAuditAnalysisRunner.Run(
            fixture,
            observerEnabled,
            ordinal,
            true,
            mode,
            parallelism,
            materialization,
            restoreAndBuild);
    }

    private static BenchmarkProfileSample AddReportMeasurement(
        BenchmarkMaterializedFixture fixture,
        BenchmarkProfileSample sample,
        BenchmarkProfilerOverheadEvidence overhead)
    {
        BenchmarkResourceMeasurement reportPublication;
        string? reportIdentity = null;
        if (fixture.Definition.Dimensions.ReportRuleCount == 0)
        {
            reportPublication = BenchmarkResourceMeasurement.NotApplicable(
                "This workload does not scale report rules and does not format a SARIF result.");
        }
        else
        {
            var publication = Stopwatch.StartNew();
            string sarif = BenchmarkSarifWorkloadFactory.FormatResult(fixture);
            publication.Stop();
            reportPublication = Milliseconds(publication.Elapsed);
            reportIdentity = BenchmarkIdentity.Sha256(BenchmarkIdentity.NormalizeJson(sarif));
        }

        BenchmarkTimingBoundaryEvidence boundaries = sample.TimingBoundaries! with
        {
            ResultPublication = reportPublication,
            ProfilerOverhead = overhead
        };
        return sample with { TimingBoundaries = boundaries, ReportOutputSha256 = reportIdentity };
    }

    private static BenchmarkResourceMeasurement Milliseconds(TimeSpan elapsed)
    {
        return BenchmarkResourceMeasurement.Available(Math.Max(1, (long)Math.Ceiling(elapsed.TotalMilliseconds)),
            "milliseconds");
    }

    private static string GetSourceRevision(string repositoryRoot)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("--verify");
        startInfo.ArgumentList.Add("HEAD");
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Could not identify the benchmark source revision: {error}");

        return output.Trim();
    }

    private static string Describe(EvaluationResults results)
    {
        return string.Join(Environment.NewLine, results.Details?.Where(detail => !detail.IsValid).Select(detail =>
            $"{detail.EvaluationPath}: {string.Join(",", detail.Errors?.Values.ToArray() ?? [])}").ToArray() ?? []);
    }
}
