using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

// CliRuntime is `internal` in ArchLinterNet.Cli, visible here via
// [InternalsVisibleTo("ArchLinterNet.Cli.Tests")]. These tests exercise CliRuntime's own thin
// forwarding methods directly, since ValidateCommandHandler-level tests (CliArchitectureTests,
// CliHandlerCoverageTests) exclusively use fake ICliRuntime implementations and never construct
// the real CliRuntime, leaving its forwarding methods otherwise uncovered.
[TestFixture]
public sealed class CliRuntimeTests
{
    [Test]
    public void FormatClassificationFactsForHumans_ForwardsClassificationPathDeferredToFormatter()
    {
        var runtime = new CliRuntime();
        var notice = new ArchitectureClassificationPathDeferredNotice(2);

        string result = runtime.FormatClassificationFactsForHumans(
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>(),
            notice);

        Assert.That(result, Does.Contain("path_deferred"));
        Assert.That(result, Does.Contain("classification.path declares 2 entries"));
    }

    [Test]
    public void FormatResultForCiArtifacts_ForwardsClassificationPathDeferredToFormatter()
    {
        var runtime = new CliRuntime();
        var notice = new ArchitectureClassificationPathDeferredNotice(1);

        string result = runtime.FormatResultForCiArtifacts(
            "strict",
            true,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureCycleFinding>(),
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            Array.Empty<PolicyConsistencyDiagnostic>(),
            Array.Empty<ArchitectureCoverageSummary>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>(),
            Array.Empty<ArchitectureClassificationRoleFact>(),
            notice,
            Array.Empty<BuildStatePreflightDiagnostic>());

        Assert.That(result, Does.Contain("\"declared_entry_count\":1"));
    }

    [Test]
    public void FormatBuildStatePreflightForHumans_ForwardsToFormatter()
    {
        var runtime = new CliRuntime();
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence("Fixture.csproj", "Fixture", BuildCommand: "dotnet build \"Fixture.csproj\""));

        string result = runtime.FormatBuildStatePreflightForHumans(new[] { diagnostic });

        Assert.That(result, Does.Contain("Build-state preflight:"));
        Assert.That(result, Does.Contain("missing-artifact"));
    }

    [Test]
    public void FormatResultAsSarif_NoCycleFindings_ForwardsPreflightDiagnosticsToInstanceFormatter()
    {
        var runtime = new CliRuntime();
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence("Fixture.csproj", "Fixture"));

        string sarif = runtime.FormatResultAsSarif(
            "strict", Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            Array.Empty<ArchitectureCycleFinding>(), new[] { diagnostic });

        Assert.That(sarif, Does.Contain("build-state-preflight/missing-artifact"));
    }

    [Test]
    public void FormatResultAsSarif_WithCycleFindings_ForwardsPreflightDiagnosticsToStaticFormatter()
    {
        var runtime = new CliRuntime();
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.StaleArtifact,
            new BuildStatePreflightEvidence("Fixture.csproj", "Fixture"));
        var cycleFinding = new ArchitectureCycleFinding("cycle-contract", null, "A -> B -> A");

        string sarif = runtime.FormatResultAsSarif(
            "strict", Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            new[] { cycleFinding }, new[] { diagnostic });

        Assert.That(sarif, Does.Contain("build-state-preflight/stale-artifact"));
    }

    [Test]
    public void MigrateBaseline_MissingBaselineFile_ForwardsToEngineAndThrows()
    {
        var runtime = new CliRuntime();

        Assert.Throws<FileNotFoundException>(() => runtime.MigrateBaseline(new BaselineMigrateRequest
        {
            PolicyPath = "unused-nonexistent-policy.yml",
            BaselinePath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}.yml"),
            DryRun = true,
        }));
    }
}
