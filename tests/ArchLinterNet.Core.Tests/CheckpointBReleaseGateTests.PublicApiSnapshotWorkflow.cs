using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    // A reviewed baseline with members the mutation can remove and change. The `small` fixture's
    // own Service exposes only a constructor, which cannot demonstrate removals or signature
    // changes, so the scenario authors its own reviewed surface in its private copy.
    private const string ReviewedApiSource = """
        namespace Synthetic.Small;

        public sealed class Service
        {
            public string Removed() => "removed";

            public int Changed(int value) => value;
        }
        """;

    // The same type after an ordinary API evolution: one member added, one removed, one signature
    // changed. #466 requires all three classes to be observed through the reviewed snapshot
    // lifecycle, driven by the installed candidate.
    private const string EvolvedApiSource = """
        namespace Synthetic.Small;

        public sealed class Service
        {
            public string Added() => "added";

            public long Changed(int value) => value;
        }
        """;

    private static readonly string[] _expectedDeltaKinds = ["added", "removed", "changed"];

    private static CheckpointScenarioResult AssertPublicApiSnapshotWorkflow(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("small");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "public-api"));
        string sourcePath = Path.Combine(fixture.Root, "Service.cs");
        File.WriteAllText(sourcePath, ReviewedApiSource);
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
        string reviewedSnapshot = File.ReadAllText(Path.Combine(fixture.Root, Snapshot));

        Assert.Multiple(() =>
        {
            Assert.That(capture.ExitCode, Is.EqualTo(0), capture.CombinedOutput);
            Assert.That(File.Exists(Path.Combine(fixture.Root, Snapshot)), Is.True);
            Assert.That(diff.ExitCode, Is.EqualTo(0), diff.CombinedOutput);
            Assert.That(update.ExitCode, Is.EqualTo(0), update.CombinedOutput);
            Assert.That(reviewedSnapshot, Does.Contain("method Synthetic.Small.Service.Removed(): System.String"));
            Assert.That(reviewedSnapshot,
                Does.Contain("method Synthetic.Small.Service.Changed(System.Int32): System.Int32"));
        });

        AssertPublicApiDeltaLifecycle(candidate, fixture, sourcePath, Snapshot);

        File.AppendAllText(sourcePath, Environment.NewLine + "// stale receipt regression");
        CommandResult stale = candidate.RunTool(fixture.Root,
            "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "small-api",
            "--snapshot", Snapshot, "--format", "json");

        Assert.Multiple(() =>
        {
            Assert.That(stale.ExitCode, Is.EqualTo(2), stale.CombinedOutput);
            Assert.That(stale.CombinedOutput, Does.Contain("stale-artifact"));
        });
        return Passed("public-api-snapshot-workflow");
    }

    // F3 — added, removed and changed public signatures must all be observed through the reviewed
    // snapshot lifecycle, and `update` must bring the snapshot back in sync.
    private static void AssertPublicApiDeltaLifecycle(
        CandidatePackageFeed candidate,
        AdoptionAcceptanceFixture fixture,
        string sourcePath,
        string snapshot)
    {
        File.WriteAllText(sourcePath, EvolvedApiSource);
        fixture.Build(configuration: "Release", targetFramework: "net10.0");

        CommandResult evolved = candidate.RunTool(fixture.Root,
            "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "small-api",
            "--snapshot", snapshot, "--ensure-built", "--format", "json");

        using JsonDocument document = JsonDocument.Parse(evolved.StandardOutput);
        Dictionary<string, string> deltas = document.RootElement.GetProperty("violations").EnumerateArray()
            .ToDictionary(
                violation => violation.GetProperty("api_delta_kind").GetString() ?? string.Empty,
                violation => violation.GetProperty("undeclared_api_signature").GetString() ?? string.Empty,
                StringComparer.Ordinal);

        CommandResult resynchronize = candidate.RunTool(fixture.Root,
            "public-api", "update", "--policy", fixture.PolicyPath, "--contract", "small-api",
            "--snapshot", snapshot, "--ensure-built", "--format", "json");
        CommandResult reviewed = candidate.RunTool(fixture.Root,
            "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "small-api",
            "--snapshot", snapshot, "--ensure-built", "--format", "json");
        string resynchronizedSnapshot = File.ReadAllText(Path.Combine(fixture.Root, snapshot));

        Assert.Multiple(() =>
        {
            Assert.That(evolved.ExitCode, Is.EqualTo(1), evolved.CombinedOutput);
            Assert.That(deltas.Keys, Is.EquivalentTo(_expectedDeltaKinds),
                $"Every delta class must be observed.{Environment.NewLine}{evolved.CombinedOutput}");
            Assert.That(deltas["added"], Is.EqualTo("method Synthetic.Small.Service.Added(): System.String"));
            Assert.That(deltas["removed"], Is.EqualTo("method Synthetic.Small.Service.Removed(): System.String"));
            Assert.That(deltas["changed"],
                Is.EqualTo("method Synthetic.Small.Service.Changed(System.Int32): System.Int64"));
            Assert.That(resynchronize.ExitCode, Is.EqualTo(0), resynchronize.CombinedOutput);
            Assert.That(resynchronizedSnapshot,
                Does.Contain("method Synthetic.Small.Service.Added(): System.String"));
            Assert.That(resynchronizedSnapshot,
                Does.Not.Contain("method Synthetic.Small.Service.Removed(): System.String"));
            Assert.That(reviewed.ExitCode, Is.EqualTo(0), reviewed.CombinedOutput);
            Assert.That(JsonDocument.Parse(reviewed.StandardOutput).RootElement
                .GetProperty("violations").GetArrayLength(), Is.Zero,
                "`update` must bring the reviewed snapshot back in sync.");
        });
    }
}
