using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void ReportPr_CanonicalHealthAndChangeArtifacts_RenderThroughBuiltCli()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-pr-report-baseline-{Guid.NewGuid():N}.yml");
        string baseSnapshotPath = Path.Combine(Path.GetTempPath(), $"architecture-pr-report-base-{Guid.NewGuid():N}.json");
        string currentSnapshotPath = Path.Combine(Path.GetTempPath(), $"architecture-pr-report-current-{Guid.NewGuid():N}.json");
        string healthPath = Path.Combine(Path.GetTempPath(), $"architecture-pr-report-health-{Guid.NewGuid():N}.json");
        string changePath = Path.Combine(Path.GetTempPath(), $"architecture-pr-report-change-{Guid.NewGuid():N}.json");
        try
        {
            var (baselineExit, _, baselineError) = RunCli(
                "baseline", "generate", "--policy", _passingPolicy, "--output", baselinePath);
            Assert.That(baselineExit, Is.EqualTo(0), $"stderr: {baselineError}");

            var (baseSnapshotExit, _, baseSnapshotError) = RunCli(
                "change", "snapshot", "--policy", _passingPolicy, "--baseline", baselinePath,
                "--mode", "strict", "--output", baseSnapshotPath);
            Assert.That(baseSnapshotExit, Is.EqualTo(0), $"stderr: {baseSnapshotError}");

            var (currentSnapshotExit, _, currentSnapshotError) = RunCli(
                "change", "snapshot", "--policy", _passingPolicy, "--baseline", baselinePath,
                "--mode", "strict", "--output", currentSnapshotPath);
            Assert.That(currentSnapshotExit, Is.EqualTo(0), $"stderr: {currentSnapshotError}");

            var (changeExit, _, changeError) = RunCli(
                "change", "report", "--base", baseSnapshotPath, "--current", currentSnapshotPath,
                "--execution-context", "run", "--format", "json", "--output", changePath);
            Assert.That(changeExit, Is.EqualTo(0), $"stderr: {changeError}");

            var (healthExit, healthJson, healthError) = RunCli(
                "health", "--policy", _passingPolicy, "--baseline", baselinePath, "--format", "json", "--execution-context", "run");
            Assert.That(healthExit, Is.EqualTo(0), $"stderr: {healthError}");
            File.WriteAllText(healthPath, healthJson);

            var (reportExit, markdown, reportError) = RunCli(
                "report", "pr", "--health", healthPath, "--change", changePath, "--max-details", "3");

            Assert.Multiple(() =>
            {
                Assert.That(reportExit, Is.EqualTo(0), $"stderr: {reportError}");
                Assert.That(markdown, Does.Contain("# Architecture PR report"));
                Assert.That(markdown, Does.Contain("Architecture acceptance: **pass**"));
                Assert.That(markdown, Does.Not.Contain("Showing 0 of 0"));
                Assert.That(reportError, Is.Empty);
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
            DeleteIfPresent(baseSnapshotPath);
            DeleteIfPresent(currentSnapshotPath);
            DeleteIfPresent(healthPath);
            DeleteIfPresent(changePath);
        }
    }
}
