using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    // #656 acceptance criterion: combined strict+audit must be equivalent to two standalone runs,
    // not merely "produce two result objects". combined-equivalence-policy.yml deliberately forbids
    // Core -> CEL under both a strict and an audit contract, so each mode reports a genuine,
    // deterministic violation here — a regression that reordered modes, dropped a finding, or
    // diverged one mode's identity inside the combined snapshot would fail this comparison, whereas
    // it would pass unnoticed against an all-passing policy.
    [Test]
    public void CombinedMode_StrictAndAuditResults_MatchStandaloneRunsExactly()
    {
        (int standaloneStrictExit, string standaloneStrictJson, string standaloneStrictErr) = RunCli(
            "--policy", _combinedEquivalencePolicy, "--mode", "strict", "--format", "json");
        (int standaloneAuditExit, string standaloneAuditJson, string standaloneAuditErr) = RunCli(
            "--policy", _combinedEquivalencePolicy, "--mode", "audit", "--format", "json");
        (int combinedExit, string combinedJson, string combinedErr) = RunCli(
            "--policy", _combinedEquivalencePolicy, "--mode", "strict,audit", "--format", "json");

        Assert.That(standaloneStrictExit, Is.Not.EqualTo(0), $"stderr: {standaloneStrictErr}");
        Assert.That(standaloneAuditExit, Is.Not.EqualTo(0), $"stderr: {standaloneAuditErr}");

        using JsonDocument combinedDocument = JsonDocument.Parse(combinedJson);
        JsonElement combinedResults = combinedDocument.RootElement.GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(combinedExit, Is.EqualTo(standaloneStrictExit), $"stderr: {combinedErr}");
            Assert.That(combinedResults.GetArrayLength(), Is.EqualTo(2));

            // Order: the combined document lists results in requested-mode order (strict, audit).
            Assert.That(combinedResults[0].GetProperty("mode").GetString(), Is.EqualTo("strict"));
            Assert.That(combinedResults[1].GetProperty("mode").GetString(), Is.EqualTo("audit"));

            Assert.That(
                JsonNode.DeepEquals(
                    JsonNode.Parse(combinedResults[0].GetRawText()),
                    JsonNode.Parse(standaloneStrictJson)),
                Is.True,
                "Combined-mode strict result must match a standalone strict run exactly (findings, identities, and order).");
            Assert.That(
                JsonNode.DeepEquals(
                    JsonNode.Parse(combinedResults[1].GetRawText()),
                    JsonNode.Parse(standaloneAuditJson)),
                Is.True,
                "Combined-mode audit result must match a standalone audit run exactly (findings, identities, and order).");
        });
    }

    [Test]
    public void CombinedMode_EnsureBuilt_ProfileCountersAndMultiSinkReportsProveOneSnapshot()
    {
        string outputDirectory = Path.Combine(
            Path.GetTempPath(), $"arch-linter-combined-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        string profilePath = Path.Combine(outputDirectory, "profile.json");
        string jsonPath = Path.Combine(outputDirectory, "result.json");
        string sarifPath = Path.Combine(outputDirectory, "result.sarif");

        try
        {
            (int exitCode, string stdout, string stderr) = RunCli(
                "--policy", _passingPolicy,
                "--mode", "strict,audit",
                "--ensure-built",
                "--profile", profilePath,
                "--report", $"json={jsonPath}",
                "--report", $"sarif={sarifPath}");

            Assert.That(exitCode, Is.EqualTo(0), $"stdout: {stdout}\nstderr: {stderr}");

            using JsonDocument profile = JsonDocument.Parse(File.ReadAllText(profilePath));
            JsonElement counters = profile.RootElement.GetProperty("Counters");
            using JsonDocument jsonReport = JsonDocument.Parse(File.ReadAllText(jsonPath));
            using JsonDocument sarifReport = JsonDocument.Parse(File.ReadAllText(sarifPath));

            Assert.Multiple(() =>
            {
                Assert.That(counters.GetProperty("PolicyCompositions").GetInt32(), Is.EqualTo(1));
                Assert.That(counters.GetProperty("ProjectGraphEvaluations").GetInt32(), Is.EqualTo(1));
                Assert.That(counters.GetProperty("ModesEvaluated").GetInt32(), Is.EqualTo(2));
                Assert.That(counters.GetProperty("SnapshotMaterializations").GetInt32(), Is.EqualTo(1));

                // The extra sinks only add rendering/output work. They do not add an analysis
                // evaluation or alter the deterministic snapshot counters above.
                Assert.That(counters.GetProperty("RenderedSinkCount").GetInt32(), Is.EqualTo(2));
                Assert.That(counters.GetProperty("OutputSinkCount").GetInt32(), Is.EqualTo(2));
                Assert.That(jsonReport.RootElement.GetProperty("results").GetArrayLength(), Is.EqualTo(2));
                Assert.That(sarifReport.RootElement.GetProperty("runs").GetArrayLength(), Is.EqualTo(2));
            });
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
