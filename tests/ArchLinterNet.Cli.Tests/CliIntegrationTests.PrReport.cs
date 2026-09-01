using ArchLinterNet.Core.Change;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void ReportPr_CanonicalHealthAndChangeArtifacts_RenderThroughBuiltCli()
    {
        string baselinePath = Path.Combine(Path.GetTempPath(), $"architecture-pr-report-baseline-{Guid.NewGuid():N}.yml");
        string healthPath = Path.Combine(Path.GetTempPath(), $"architecture-pr-report-health-{Guid.NewGuid():N}.json");
        string changePath = Path.Combine(Path.GetTempPath(), $"architecture-pr-report-change-{Guid.NewGuid():N}.json");
        try
        {
            var (baselineExit, _, baselineError) = RunCli(
                "baseline", "generate", "--policy", _passingPolicy, "--output", baselinePath);
            Assert.That(baselineExit, Is.EqualTo(0), $"stderr: {baselineError}");

            var (healthExit, healthJson, healthError) = RunCli(
                "health", "--policy", _passingPolicy, "--baseline", baselinePath, "--format", "json");
            Assert.That(healthExit, Is.EqualTo(0), $"stderr: {healthError}");
            File.WriteAllText(healthPath, healthJson);
            File.WriteAllText(changePath, ArchitectureChangeReports.FormatJson(
                new ArchitectureChangeReport([], [], [], [], [])));

            var (reportExit, markdown, reportError) = RunCli(
                "report", "pr", "--health", healthPath, "--change", changePath, "--max-details", "3");

            Assert.Multiple(() =>
            {
                Assert.That(reportExit, Is.EqualTo(0), $"stderr: {reportError}");
                Assert.That(markdown, Does.Contain("# Architecture PR report"));
                Assert.That(markdown, Does.Contain("Architecture acceptance: **pass**"));
                Assert.That(markdown, Does.Contain("## Architecture change"));
                Assert.That(reportError, Is.Empty);
            });
        }
        finally
        {
            DeleteIfPresent(baselinePath);
            DeleteIfPresent(healthPath);
            DeleteIfPresent(changePath);
        }
    }
}
