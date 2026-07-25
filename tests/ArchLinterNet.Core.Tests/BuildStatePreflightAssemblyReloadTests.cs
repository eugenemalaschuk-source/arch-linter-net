using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Regression coverage for post-ensure-built loading: a rebuilt target must be read from an
// isolated snapshot scope, never reused from a same-simple-name assembly already in the process.
[TestFixture]
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
        File.WriteAllText(Path.Combine(projectDir, "Class1.cs"), "namespace ReloadFixture; public class MarkerV1 {}");

        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test

            analysis:
              target_assemblies: [ReloadFixture]
              projects: ["src/ReloadFixture/ReloadFixture.csproj"]
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

        // Change the source so the rebuilt assembly's exported type set changes.
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

        System.Reflection.Assembly reloadedAssembly = snapshot.Runner.Session.Context.TargetAssemblies.Single();
        string[] typeNames = reloadedAssembly.GetTypes().Select(t => t.Name).ToArray();

        Assert.That(typeNames, Does.Contain("MarkerV2"));
        Assert.That(typeNames, Does.Not.Contain("MarkerV1"));
    }
}
