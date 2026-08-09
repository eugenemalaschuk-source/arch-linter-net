using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private static CheckpointScenarioResult AssertPublicApiSnapshotWorkflow(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("small");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "public-api"));
        File.AppendAllText(fixture.PolicyPath, """

  configuration: Release
  target_framework: net10.0

contracts:
  strict_public_api_surface:
    - id: small-api
      name: small-api
      assemblies: [Synthetic.Small]
      api_snapshot: public-api/small-api.txt
      api_comparison: exact
      reason: The synthetic package fixture captures its reviewed public API.
""");

        // An ordinary build deliberately creates no product receipt. The installed candidate's
        // capture command must make that otherwise-unverifiable state reachable via --ensure-built.
        fixture.Build(configuration: "Release", targetFramework: "net10.0");
        const string Snapshot = "public-api/small-api.txt";
        CommandResult capture = candidate.RunTool(fixture.Root,
            "public-api", "capture", "--policy", fixture.PolicyPath, "--contract", "small-api",
            "--output", Snapshot, "--ensure-built", "--format", "json");
        CommandResult diff = candidate.RunTool(fixture.Root,
            "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "small-api",
            "--snapshot", Snapshot, "--format", "json");
        CommandResult update = candidate.RunTool(fixture.Root,
            "public-api", "update", "--policy", fixture.PolicyPath, "--contract", "small-api",
            "--snapshot", Snapshot, "--dry-run", "--format", "json");

        string sourcePath = Path.Combine(fixture.Root, "Service.cs");
        File.AppendAllText(sourcePath, Environment.NewLine + "// stale receipt regression");
        CommandResult stale = candidate.RunTool(fixture.Root,
            "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "small-api",
            "--snapshot", Snapshot, "--format", "json");

        Assert.Multiple(() =>
        {
            Assert.That(capture.ExitCode, Is.EqualTo(0), capture.CombinedOutput);
            Assert.That(File.Exists(Path.Combine(fixture.Root, Snapshot)), Is.True);
            Assert.That(diff.ExitCode, Is.EqualTo(0), diff.CombinedOutput);
            Assert.That(update.ExitCode, Is.EqualTo(0), update.CombinedOutput);
            Assert.That(stale.ExitCode, Is.EqualTo(2), stale.CombinedOutput);
            Assert.That(stale.CombinedOutput, Does.Contain("StaleArtifact"));
        });
        return Passed("public-api-snapshot-workflow");
    }
}
