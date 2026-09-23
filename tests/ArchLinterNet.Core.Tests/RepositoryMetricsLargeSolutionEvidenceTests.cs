using System.Diagnostics;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Explicit("Hardware-sensitive #990/#502 evidence harness — run manually; never in CI.")]
[Category("Benchmark")]
[CancelAfter(900_000)]
public sealed class RepositoryMetricsLargeSolutionEvidenceTests
{
    [Test]
    public void GenerateBaselineAndHeadEvidence()
    {
        string outputDirectory = Environment.GetEnvironmentVariable("ARCHLINERNET_990_EVIDENCE_DIR")
            ?? Path.Combine(Path.GetTempPath(), "arch-linter-net-990-evidence");
        string baselineCli = Environment.GetEnvironmentVariable("ARCHLINERNET_990_BASELINE_CLI")
            ?? throw new InvalidOperationException("ARCHLINERNET_990_BASELINE_CLI must point to the baseline CLI DLL.");
        string headCli = CliDllPath();
        Directory.CreateDirectory(outputDirectory);

        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-very-large-multi-project",
            BenchmarkTopologyShape.Dense,
            new BenchmarkDimensionSet
            {
                ProjectCount = 32,
                TypesPerProject = 16,
                SourceFilesPerProject = 8,
                ReferencesPerProject = 31,
                LayerCount = 8,
                SelectorPredicateTermsPerLayer = 16,
                ContractsPerWorkload = 8,
                FindingCandidates = 128,
                SourceRootCount = 4,
            },
            BenchmarkCompilationMode.RealMsBuild,
            BenchmarkExecutionMode.FullGovernance,
            independentProcesses: 6);
        using BenchmarkMaterializedFixture baselineFixture = BenchmarkFixtureMaterializer.Materialize(workload);
        using BenchmarkMaterializedFixture headFixture = BenchmarkFixtureMaterializer.Materialize(workload);
        File.Copy(
            Path.Combine(baselineFixture.Root, ".benchmark", "workload.json"),
            Path.Combine(outputDirectory, "workload.json"),
            overwrite: true);

        RunCli(baselineFixture, baselineCli, Path.Combine(outputDirectory, "baseline-profile.json"),
            Path.Combine(outputDirectory, "baseline-report.json"), Path.Combine(outputDirectory, "baseline-error.log"),
            expectedExitCode: 1);
        RunCli(headFixture, headCli, Path.Combine(outputDirectory, "head-profile.json"),
            Path.Combine(outputDirectory, "head-report.json"), Path.Combine(outputDirectory, "head-error.log"),
            expectedExitCode: 1);

        TestContext.Out.WriteLine($"Evidence directory: {outputDirectory}");
        TestContext.Out.WriteLine($"Workload: {workload.WorkloadId}");
        TestContext.Out.WriteLine($"Projects: {workload.Inventory.ProjectCount}");
        TestContext.Out.WriteLine($"Source files: {workload.Inventory.SourceFileCount}");
        TestContext.Out.WriteLine($"Types: {workload.Inventory.TypeCount}");
        TestContext.Out.WriteLine($"Reference edges: {workload.Inventory.ReferenceEdgeCount}");
    }

    private static string CliDllPath()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        return Path.Combine(repositoryRoot, "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");
    }

    private static void RunCli(
        BenchmarkMaterializedFixture fixture,
        string cliPath,
        string profilePath,
        string reportPath,
        string errorPath,
        int expectedExitCode)
    {
        Assert.That(File.Exists(cliPath), Is.True, $"CLI not found at {cliPath}.");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = fixture.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (string argument in new[]
        {
            cliPath,
            "--policy", fixture.PolicyPath,
            "--mode", "strict",
            "--format", "json",
            "--ensure-built",
            "--profile", profilePath,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        File.WriteAllText(reportPath, output);
        File.WriteAllText(errorPath, error);
        Assert.That(process.ExitCode, Is.EqualTo(expectedExitCode), $"CLI failed: {cliPath}{Environment.NewLine}{error}");
        Assert.That(File.Exists(profilePath), Is.True, $"CLI did not write profile: {profilePath}");
        using JsonDocument report = JsonDocument.Parse(output);
        Assert.That(report.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
    }
}
