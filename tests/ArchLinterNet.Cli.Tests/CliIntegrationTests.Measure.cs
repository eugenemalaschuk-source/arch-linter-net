using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void Measure_Help_ShowsMeasureSpecificOptionsAndExitsZero()
    {
        var (exitCode, stdout, stderr) = RunCli("measure", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("report declared architecture metrics"));
            Assert.That(stdout, Does.Contain("--metric"));
            Assert.That(stdout, Does.Contain("--all-contributors"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public void Measure_CompleteMetric_EmitsVersionedJsonAndExitsZero()
    {
        string policy = Path.Combine(_repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "metrics-policy.yml");
        var (exitCode, stdout, stderr) = RunCli("measure", "--policy", policy, "--format", "json");

        Assert.That(exitCode, Is.EqualTo(0), $"stderr: {stderr}");

        using JsonDocument document = JsonDocument.Parse(stdout);
        JsonElement[] measurements = document.RootElement.GetProperty("measurements").EnumerateArray().ToArray();
        JsonElement measurement = measurements.Single();
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("schema_id").GetString(),
                Is.EqualTo("architecture-metrics-report/v1"));
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("complete"));
            Assert.That(measurement.GetProperty("id").GetString(), Is.EqualTo("model-external-groups"));
            Assert.That(measurement.GetProperty("value").GetInt32(), Is.Zero);
            Assert.That(measurement.GetProperty("contributors").GetArrayLength(), Is.Zero);
            Assert.That(document.RootElement.GetProperty("applicability_findings").GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public void Measure_UnassessableMetric_EmitsTypedReportAndExitsTwo()
    {
        string policy = Path.Combine(
            _repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "metrics-unassessable-policy.yml");
        var (exitCode, stdout, stderr) = RunCli("measure", "--policy", policy, "--format", "json");

        Assert.That(exitCode, Is.EqualTo(2), $"stderr: {stderr}");

        using JsonDocument document = JsonDocument.Parse(stdout);
        JsonElement[] measurements = document.RootElement.GetProperty("measurements").EnumerateArray().ToArray();
        JsonElement measurement = measurements.Single();
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("unassessable"));
            Assert.That(measurement.GetProperty("id").GetString(), Is.EqualTo("execution-outgoing"));
            Assert.That(measurement.GetProperty("state").GetString(), Is.EqualTo("unassessable"));
            Assert.That(measurement.GetProperty("value").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(measurement.GetProperty("contributors").GetArrayLength(), Is.Zero);
            Assert.That(document.RootElement.GetProperty("applicability_findings").GetArrayLength(), Is.GreaterThan(0));
        });
    }

    [Test]
    public void Measure_UnknownMetric_ReportsConfigurationErrorAndExitsTwo()
    {
        string policy = Path.Combine(_repoRoot, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "metrics-policy.yml");
        var (exitCode, stdout, stderr) = RunCli(
            "measure", "--policy", policy, "--format", "json", "--metric", "missing-metric");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(stderr, Is.Empty);
            Assert.That(stdout, Does.Contain("Unknown metric IDs: missing-metric."));
        });
    }
}
