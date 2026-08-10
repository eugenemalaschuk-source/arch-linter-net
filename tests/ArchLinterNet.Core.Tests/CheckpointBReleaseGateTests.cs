using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("E2E")]
[Category("ReleaseGate")]
[CancelAfter(300_000)]
public sealed partial class CheckpointBReleaseGateTests
{
    private const string CandidateVersionEnvironmentVariable = "CHECKPOINT_B_CANDIDATE_VERSION";
    private const string DefaultCandidateVersion = "0.6.1";

    // The public adoption package line this candidate belongs to, and the packaged schema
    // generation carrying its policy-root identity. These are release-line constants; the
    // candidate's own package version may additionally carry a prerelease or build suffix.
    private const string ProductReleaseLine = "0.6.1";
    private const string ProductSchemaGeneration = "0.6.1";
    private static readonly string[] _packageIds =
        ["ArchLinterNet.CEL", "ArchLinterNet.Cli", "ArchLinterNet.Core", "ArchLinterNet.Testing"];

    [Test]
    public void PackedCandidate_InstallsFromAnIsolatedFeedAndPassesTheSyntheticAdopterMatrix()
    {
        using CandidatePackageFeed candidate = CandidatePackageFeed.Create();

        CheckpointScenarioResult packageProvenance = candidate.AssertPackageProvenance();
        candidate.InstallTool();
        candidate.AssertOfflineSchemaRegistry();
        CheckpointScenarioResult cancellation = candidate.AssertExternalTestingConsumer();
        AssertCleanCheckoutOracle(candidate);

        var scenarios = new List<CheckpointScenarioResult>
        {
            packageProvenance,
            candidate.AssertOfflineSchemaRegistry(),
            cancellation,
            AssertCleanCheckoutOracle(candidate),
            candidate.AssertGenericCiNeutralInvocation(),
            candidate.AssertDocumentedEntrypoint(),
            candidate.AssertNonTtyInvocation(),
        };
        foreach (string fixtureId in new[] { "small", "multi-project", "multi-host", "migration", "aspnet-host" })
        {
            using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create(fixtureId);
            fixture.Build();

            CommandResult sequential = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--ensure-built",
                "--max-parallelism", "1");
            AssertFixtureOracle(fixtureId, sequential);
            CommandResult defaultParallelism = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--ensure-built");
            string profilePath = Path.Combine(fixture.Root, "checkpoint-b-profile.json");
            CommandResult profiled = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--ensure-built",
                "--profile", profilePath);

            Assert.Multiple(() =>
            {
                Assert.That(sequential.ExitCode, Is.EqualTo(defaultParallelism.ExitCode), fixtureId);
                Assert.That(CanonicalJson(sequential.StandardOutput),
                    Is.EqualTo(CanonicalJson(defaultParallelism.StandardOutput)), fixtureId);
                Assert.That(profiled.ExitCode, Is.EqualTo(sequential.ExitCode), fixtureId);
                Assert.That(CanonicalJson(profiled.StandardOutput),
                    Is.EqualTo(CanonicalJson(sequential.StandardOutput)), fixtureId);
                Assert.That(File.Exists(profilePath), Is.True, profilePath);
                Assert.That(sequential.StandardError, Does.Not.Contain("\u001b["), fixtureId);
            });
        }

        scenarios.Add(AssertPublicApiSnapshotWorkflow(candidate));
        AssertCacheLifecycleOracle(candidate);
        scenarios.Add(candidate.AssertMissingSharedFrameworkDiagnostic());
        scenarios.Add(Passed("sequential-default-parity"));
        scenarios.Add(Passed("profile-generation"));
        scenarios.Add(Passed("cache-miss-population-hit"));
        scenarios.Add(Passed("cache-corruption-recompute"));
        scenarios.AddRange(candidate.ShellScenarios());
        scenarios.Add(candidate.AssertCliInFlightCancellation());
        scenarios.AddRange(AssertConsumerCleanupMatrix(candidate, out ConsumerPolicyShape policyShape));
        candidate.WriteEvidence(scenarios, policyShape);
    }

    private static CheckpointScenarioResult Passed(string id) => new(id, "passed", null);

    private static void AssertFixtureOracle(string fixtureId, CommandResult result)
    {
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        JsonElement root = document.RootElement;
        JsonElement findings = root.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : root.GetProperty("findings");
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0), $"{fixtureId} must complete successfully.{Environment.NewLine}{result.CombinedOutput}");
            Assert.That(findings.ValueKind, Is.EqualTo(JsonValueKind.Array), fixtureId);
            Assert.That(findings.GetArrayLength(), Is.Zero, $"{fixtureId} must have no findings.");
        });
    }

    private static CheckpointScenarioResult AssertCleanCheckoutOracle(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("clean-checkout");
        CommandResult result = candidate.RunTool(fixture.Root,
            "--policy", fixture.PolicyPath,
            "--strict",
            "--format", "json");
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1), result.CombinedOutput);
            Assert.That(result.CombinedOutput, Does.Contain("MissingArtifact"));
            Assert.That(Directory.GetDirectories(fixture.Root, "bin", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.GetDirectories(fixture.Root, "obj", SearchOption.AllDirectories), Is.Empty);
        });
        return Passed("clean-checkout");
    }

    private static void AssertCacheLifecycleOracle(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("multi-project");
        fixture.Build();
        string cachePath = Path.Combine(fixture.Root, ".checkpoint-b-cache");
        string firstProfile = Path.Combine(fixture.Root, "cache-first-profile.json");
        string secondProfile = Path.Combine(fixture.Root, "cache-second-profile.json");
        CommandResult first = candidate.RunTool(fixture.Root, "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built", "--cache", cachePath, "--profile", firstProfile);
        CommandResult second = candidate.RunTool(fixture.Root, "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built", "--cache", cachePath, "--profile", secondProfile);
        string[] entries = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
        Assert.Multiple(() =>
        {
            AssertFixtureOracle(fixture.Id, first);
            AssertFixtureOracle(fixture.Id, second);
            Assert.That(ProfileCounter(firstProfile, "Misses"), Is.GreaterThan(0));
            Assert.That(ProfileCounter(firstProfile, "Writes"), Is.GreaterThan(0));
            Assert.That(ProfileCounter(secondProfile, "Hits"), Is.GreaterThan(0));
            Assert.That(entries, Is.Not.Empty, "A cache-eligible fixture must create a cache entry.");
        });
        string entry = entries.Single(path => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase));
        File.WriteAllText(entry, "corrupt-checkpoint-b-entry");
        string corruptedProfile = Path.Combine(fixture.Root, "cache-corruption-profile.json");
        CommandResult afterCorruption = candidate.RunTool(fixture.Root, "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built", "--cache", cachePath, "--profile", corruptedProfile);
        Assert.Multiple(() =>
        {
            AssertFixtureOracle(fixture.Id, afterCorruption);
            Assert.That(ProfileCounter(corruptedProfile, "CorruptionEvents"), Is.GreaterThan(0));
            Assert.That(ProfileCounter(corruptedProfile, "Hits"), Is.Zero);
        });
    }

    private static int ProfileCounter(string path, string counter)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("Counters").GetProperty("Cache").GetProperty(counter).GetInt32();
    }

    private static string CanonicalJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement);
    }

    private static string CanonicalFindingsJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement findings = root.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : root.GetProperty("findings");
        return JsonSerializer.Serialize(findings);
    }

    private static CommandResult RunDotnet(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Run(startInfo);
    }

    private static CommandResult Run(ProcessStartInfo startInfo)
    {
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{startInfo.FileName}'.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => $"stdout:{Environment.NewLine}{StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{StandardError}";
    }

    private sealed record PackageEvidence(string Id, string Version, string File, long Size, string Sha256);

    private sealed record CheckpointScenarioResult(string Id, string Result, string? Reason);
}
