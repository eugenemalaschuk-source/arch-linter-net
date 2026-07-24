using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Exercises build-state preflight through the real, fully composed pipeline
// (ArchitectureValidationService -> real IArchitectureRunnerSetupService -> real project
// discovery/assembly resolution), not fakes — proving preflight is actually reachable for a clean
// checkout instead of the legacy discovery/resolution path throwing before it runs.
[TestFixture]
public sealed class BuildStatePreflightCleanCheckoutTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-clean-checkout-{Guid.NewGuid():N}");
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
    public void TargetAssembliesWithDiscoveredProject_CleanCheckout_BlocksWithMissingArtifactWithoutThrowing()
    {
        string projectDir = Path.Combine(_tempDir, "src", "Fixture");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(
            Path.Combine(projectDir, "Fixture.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test

            analysis:
              target_assemblies: [Fixture]
              projects: ["src/Fixture/Fixture.csproj"]
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Passed, Is.False);
            Assert.That(outcome.PreflightBlocked, Is.True);
            BuildStatePreflightDiagnostic diagnostic = outcome.PreflightDiagnostics.Single();
            Assert.That(diagnostic.State, Is.EqualTo(BuildStatePreflightState.MissingArtifact));
            Assert.That(diagnostic.Evidence.BuildCommand, Does.Contain("dotnet build"));
            Assert.That(diagnostic.Evidence.BuildCommand, Does.Contain("Fixture.csproj"));
        });
    }

    [Test]
    public void ProjectGraphOnlyWithoutTargetAssembliesOrCoverageTolerance_CleanCheckout_BlocksWithMissingArtifactWithoutThrowing()
    {
        // The primary #362 acceptance scenario in its purest form: a project graph with no
        // analysis.target_assemblies configured at all and no project-scope coverage contract to
        // make resolution tolerant of unresolved projects — previously this threw an untyped
        // "Architecture YAML must define analysis.target_assemblies" InvalidOperationException
        // (see ArchitectureAssemblyResolutionService.Resolve) before build-state preflight had any
        // chance to run. It now reports the discovered project's assembly name as missing so
        // preflight can emit its own typed diagnostic instead.
        string projectDir = Path.Combine(_tempDir, "src", "Fixture");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(
            Path.Combine(projectDir, "Fixture.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test

            analysis:
              projects: ["src/Fixture/Fixture.csproj"]
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Passed, Is.False);
            Assert.That(outcome.PreflightBlocked, Is.True);
            BuildStatePreflightDiagnostic diagnostic = outcome.PreflightDiagnostics.Single();
            Assert.That(diagnostic.State, Is.EqualTo(BuildStatePreflightState.MissingArtifact));
            Assert.That(diagnostic.Evidence.ProjectPath, Is.EqualTo("src/Fixture/Fixture.csproj"));
        });
    }

    [Test]
    public void ProjectGraphOnlyWithCoverageTolerance_CleanCheckout_DoesNotPreflightBlock()
    {
        // No analysis.target_assemblies: resolution is deliberately skipped in favor of the
        // project-scope coverage contract classifying the unresolved project as "unknown" — this
        // is pre-existing, load-bearing behavior (see CoverageContractReservedTests) that build-
        // state preflight must not override just because a project was discovered.
        string projectDir = Path.Combine(_tempDir, "src", "Fixture");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(
            Path.Combine(projectDir, "Fixture.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test

            analysis:
              projects: ["src/Fixture/Fixture.csproj"]

            contracts:
              strict_coverage:
                - id: project-coverage
                  name: project-coverage
                  scope: project
                  reason: Every discovered project must be mapped or excluded.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.PreflightBlocked, Is.False);
            Assert.That(outcome.PreflightDiagnostics, Is.Empty);
            Assert.That(outcome.CoverageFindings, Has.Count.EqualTo(1));
        });
    }
}
