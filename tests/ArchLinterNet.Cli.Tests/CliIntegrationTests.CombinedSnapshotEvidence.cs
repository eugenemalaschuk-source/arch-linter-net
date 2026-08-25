using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
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
