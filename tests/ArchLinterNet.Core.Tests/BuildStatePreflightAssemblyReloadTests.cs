using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Regression coverage for post-ensure-built loading: a rebuilt target must be read from an
// isolated snapshot scope, never reused from a same-simple-name assembly already in the process.
[TestFixture]
[Category("E2E")]
public sealed class BuildStatePreflightAssemblyReloadTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-reload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    [Category("Integration")]
    [CancelAfter(180_000)]
    // Receipt verification still opens the first artifact in the default context on Windows, so
    // that platform cannot overwrite the same path for the second build in-process.
    [Platform(Exclude = "Win", Reason = "Assembly.LoadFrom locks the .dll for the process lifetime; a second same-process rebuild can't overwrite it.")]
    public void EnsureBuiltAfterStaleRebuild_ContractsSeeFreshTypeNotStaleType()
    {
        string projectDir = Path.Combine(_tempDir, "src", "ReloadFixture");
        Directory.CreateDirectory(projectDir);
        string projectPath = Path.Combine(projectDir, "ReloadFixture.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(projectDir, "Class1.cs"),
            "namespace ReloadFixture; public class MarkerV1 : System.Exception {}");

        string staleDirectory = Path.Combine(_tempDir, "stale");
        Directory.CreateDirectory(staleDirectory);

        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test

            analysis:
              target_assemblies: [ReloadFixture]
              projects: ["src/ReloadFixture/ReloadFixture.csproj"]
              assembly_search_paths: ["stale"]
            contracts:
              strict_inheritance:
                - name: fresh-artifact-must-not-inherit-exception
                  source_namespaces: [ReloadFixture]
                  forbidden_base_types: [System.Exception]
                  reason: Proves the post-build snapshot did not load the stale copy.
            """);

        // First ensure-built produces MarkerV1 and leaves a same-simple-name assembly reachable
        // from the process. The second snapshot must nevertheless analyse MarkerV2.
        ValidationOutcome firstOutcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
            PreparationMode = ArchLinterNet.Core.BuildState.BuildPreparationMode.EnsureBuilt,
            RequestedConfiguration = "Debug",
        });
        Assert.That(firstOutcome.PreflightBlocked, Is.False, () => string.Join("; ", firstOutcome.PreflightDiagnostics.Select(d => d.Evidence.Detail)));

        string firstOutputPath = Path.Combine(projectDir, "bin", "Debug", "net10.0", "ReloadFixture.dll");
        File.Copy(firstOutputPath, Path.Combine(staleDirectory, "ReloadFixture.dll"));

        // Build a clean artifact whose contract result differs from the competing stale copy.
        File.WriteAllText(Path.Combine(projectDir, "Class1.cs"), "namespace ReloadFixture; public class MarkerV2 {}");

        using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
        using ArchitectureAnalysisSnapshot snapshot = engine.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = policyPath,
            PreparationMode = ArchLinterNet.Core.BuildState.BuildPreparationMode.EnsureBuilt,
            RequestedConfiguration = "Debug",
        });
        ValidationOutcome secondOutcome = snapshot.Evaluate("strict");

        Assert.That(secondOutcome.PreflightBlocked, Is.False,
            () => string.Join("; ", secondOutcome.PreflightDiagnostics.Select(d => d.Evidence.Detail)));
        Assert.That(secondOutcome.Passed, Is.True, () => string.Join("; ", secondOutcome.Violations));
    }
}
