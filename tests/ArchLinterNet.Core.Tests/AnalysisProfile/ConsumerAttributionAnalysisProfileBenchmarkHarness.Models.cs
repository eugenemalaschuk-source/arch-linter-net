using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ConsumerAttributionAnalysisProfileBenchmarkHarness
{
    private sealed record ProcessSample(
        double CommandTotalMs,
        double PreflightMs,
        double BuildStatePreflightMs,
        string PreparationMode,
        double AnalysisOnlyMs,
        double OutputMs,
        double ProcessEnvelopeMs,
        string CompletionStatus,
        int ExitCode,
        bool OutputFailed,
        string CanonicalResultSha256,
        CounterTotals Counters,
        JsonElement Profile);

    private sealed record BatchSample(
        int ProcessCount,
        string DispatchMode,
        double OuterWallClockMs,
        double AggregateCommandTotalMs,
        double AggregatePreflightMs,
        double AggregateBuildStatePreflightMs,
        double AggregateAnalysisOnlyMs,
        double AggregateOutputMs,
        double AggregateProcessEnvelopeMs,
        CounterTotals Counters,
        string CanonicalResultSha256,
        IReadOnlyList<ProcessSample> Processes);

    private sealed record ScenarioSeries(
        IReadOnlyList<BatchSample> MeasuredSamples,
        IReadOnlyList<BatchSample> PrimingSamples);

    private sealed record ScenarioSummary(
        string ScenarioId,
        string Description,
        int SampleCount,
        double MedianAggregateCommandTotalMs,
        double P95AggregateCommandTotalMs,
        double MedianAggregateAnalysisOnlyMs,
        double P95AggregateAnalysisOnlyMs,
        double MedianOuterWallClockMs,
        double P95OuterWallClockMs,
        double MedianAggregateProcessEnvelopeMs,
        IReadOnlyList<BatchSample> Samples,
        IReadOnlyList<BatchSample> PrimingSamples);

    private sealed record CounterTotals(
        int PolicyCompositions,
        int ProjectGraphEvaluations,
        int AssemblyLoads,
        int DiscoveredProjectCount,
        int RetainedAssemblyCount,
        int SelectedAssemblyCount,
        int ModesEvaluated,
        int SnapshotMaterializations,
        int FactIndexMaterializations,
        int SourceScanPasses,
        int SourceFilesScanned,
        int ContractExecutions,
        int ContractResults)
    {
        public static CounterTotals From(JsonElement profile)
        {
            JsonElement counters = profile.GetProperty("Counters");
            return new CounterTotals(
                counters.GetProperty("PolicyCompositions").GetInt32(),
                counters.GetProperty("ProjectGraphEvaluations").GetInt32(),
                counters.GetProperty("AssemblyLoads").GetInt32(),
                counters.GetProperty("DiscoveredProjectCount").GetInt32(),
                counters.GetProperty("RetainedAssemblyCount").GetInt32(),
                counters.GetProperty("SelectedAssemblyCount").GetInt32(),
                counters.GetProperty("ModesEvaluated").GetInt32(),
                counters.GetProperty("SnapshotMaterializations").GetInt32(),
                counters.GetProperty("FactIndexMaterializations").GetInt32(),
                counters.GetProperty("SourceScanPasses").GetInt32(),
                counters.GetProperty("SourceFilesScanned").GetInt32(),
                SumObject(counters.GetProperty("ContractFamilyCounts")),
                SumObject(counters.GetProperty("ContractFamilyResultCounts")));
        }

        public static CounterTotals Sum(IEnumerable<CounterTotals> counters) => counters.Aggregate(
            new CounterTotals(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), Add);

        private static CounterTotals Add(CounterTotals left, CounterTotals right) => new(
            left.PolicyCompositions + right.PolicyCompositions,
            left.ProjectGraphEvaluations + right.ProjectGraphEvaluations,
            left.AssemblyLoads + right.AssemblyLoads,
            left.DiscoveredProjectCount + right.DiscoveredProjectCount,
            left.RetainedAssemblyCount + right.RetainedAssemblyCount,
            left.SelectedAssemblyCount + right.SelectedAssemblyCount,
            left.ModesEvaluated + right.ModesEvaluated,
            left.SnapshotMaterializations + right.SnapshotMaterializations,
            left.FactIndexMaterializations + right.FactIndexMaterializations,
            left.SourceScanPasses + right.SourceScanPasses,
            left.SourceFilesScanned + right.SourceFilesScanned,
            left.ContractExecutions + right.ContractExecutions,
            left.ContractResults + right.ContractResults);

        private static int SumObject(JsonElement value) => value.EnumerateObject().Sum(property => property.Value.GetInt32());
    }

    private sealed record BenchmarkEvidence(
        string SchemaId,
        string Issue,
        EnvironmentIdentity Environment,
        FixtureIdentity Fixture,
        BuildPreparation BuildPreparation,
        IReadOnlyList<ScenarioSummary> Scenarios,
        AttributionDecision Attribution,
        IReadOnlyList<RoutingDecision> Routing,
        string MeasurementBoundary);

    private sealed record EnvironmentIdentity(
        string OperatingSystem,
        string Runtime,
        string Architecture,
        int ProcessorCount,
        string CliFileVersion,
        string CliAssemblySha256,
        string CliPackageId,
        string CliPackageVersion,
        string CliPackageSha256,
        string CliPackageAssemblySha256,
        string SourceCommit,
        string Configuration)
    {
        public static EnvironmentIdentity Create()
        {
            string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
            string cliPath = CliDllPath();
            string cliAssemblySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(cliPath)));
            PackageIdentity package = PackageIdentity.Create(repositoryRoot, cliAssemblySha256);
            return new EnvironmentIdentity(
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                Environment.ProcessorCount,
                FileVersionInfo.GetVersionInfo(cliPath).FileVersion ?? "unknown",
                cliAssemblySha256,
                package.Id,
                package.Version,
                package.Sha256,
                package.AssemblySha256,
                Environment.GetEnvironmentVariable("ARCH_LINTER_SOURCE_SHA") ?? "unknown",
                "Release");
        }
    }

    private sealed record PackageIdentity(string Id, string Version, string Sha256, string AssemblySha256)
    {
        private const string PackageId = "ArchLinterNet.Cli";
        private const string PackageExtension = ".nupkg";

        public static PackageIdentity Create(string repositoryRoot, string launchedAssemblySha256)
        {
            string packagesDirectory = Path.Combine(repositoryRoot, "nupkg");
            string[] packages = Directory.EnumerateFiles(packagesDirectory, $"{PackageId}.*{PackageExtension}")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            if (packages.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one {PackageId} package in {packagesDirectory}, but found {packages.Length}. Run `rtk make pack` before recording evidence.");
            }

            string packagePath = packages[0];
            string fileName = Path.GetFileName(packagePath);
            string version = fileName[PackageId.Length..^PackageExtension.Length].TrimStart('.');
            using ZipArchive archive = ZipFile.OpenRead(packagePath);
            ZipArchiveEntry assemblyEntry = archive.GetEntry("tools/net10.0/any/ArchLinterNet.Cli.dll")
                ?? throw new InvalidOperationException(
                    $"Package '{packagePath}' does not contain tools/net10.0/any/ArchLinterNet.Cli.dll.");
            string packageAssemblySha256;
            using (Stream assemblyStream = assemblyEntry.Open())
            {
                packageAssemblySha256 = Convert.ToHexStringLower(SHA256.HashData(assemblyStream));
            }

            if (!StringComparer.Ordinal.Equals(packageAssemblySha256, launchedAssemblySha256))
            {
                throw new InvalidOperationException(
                    $"Package '{packagePath}' contains CLI assembly SHA-256 '{packageAssemblySha256}', " +
                    $"but the harness launches '{launchedAssemblySha256}'. Run `rtk make pack` before recording evidence.");
            }

            return new PackageIdentity(
                PackageId,
                version,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(packagePath))),
                packageAssemblySha256);
        }
    }

    private sealed record FixtureIdentity(string Id, int ProjectCount, int SourceFileCount, string Description);

    private sealed record BuildPreparation(
        double FixtureBuildWallClockMs,
        double EnsureBuiltPrimingOuterWallClockMs,
        double EnsureBuiltPrimingInnerCommandMs,
        string Description);

    private sealed record AttributionDecision(string State, string Conclusion, string Limitation);

    private sealed record RoutingDecision(string Owner, string Decision);
}
