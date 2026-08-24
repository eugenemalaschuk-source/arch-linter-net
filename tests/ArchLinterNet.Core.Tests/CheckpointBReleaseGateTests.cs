using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("E2E")]
[Category("ReleaseGate")]
[CancelAfter(300_000)]
[NonParallelizable]
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

    private CandidatePackageFeed? _candidate;

    [OneTimeSetUp]
    public void PrepareCandidate()
    {
        _candidate = CandidatePackageFeed.Create();
        _candidate.InstallTool();
    }

    [OneTimeTearDown]
    public void DisposeCandidate()
    {
        _candidate?.Dispose();
        _candidate = null;
    }

    [Test]
    public void PackedCandidate_PackageAndEntrypoints()
    {
        CandidatePackageFeed candidate = Candidate;
        var scenarios = new List<CheckpointScenarioResult>
        {
            candidate.AssertPackageProvenance(),
            candidate.AssertOfflineSchemaRegistry(),
            candidate.AssertExternalTestingConsumer(),
            AssertCleanCheckoutOracle(candidate),
            candidate.AssertGenericCiNeutralInvocation(),
            candidate.AssertDocumentedEntrypoint(),
            candidate.AssertNonTtyInvocation(),
        };
        scenarios.AddRange(candidate.ShellScenarios());
        scenarios.Add(candidate.AssertCliInFlightCancellation());
        candidate.WriteShardEvidence("package-and-entrypoints", scenarios);
    }

    [Test]
    [Platform("Win")]
    public void PackedCandidate_EnsureBuiltReplacesTestingOutput()
    {
        CandidatePackageFeed candidate = Candidate;
        candidate.WriteShardEvidence("ensure-built-replaces-testing-output",
            [candidate.AssertInstalledTestingOutputEnsureBuilt()]);
    }

    [Test]
    public void PackedCandidate_AdopterRuntimeCore()
    {
        CandidatePackageFeed candidate = Candidate;
        AssertAdopterRuntimeFixtures(candidate, ["small", "multi-project", "multi-host"]);
        candidate.WriteShardEvidence("adopter-runtime-core",
            [Passed("sequential-default-parity"), Passed("profile-generation")]);
    }

    [Test]
    public void PackedCandidate_AdopterRuntimeExtended()
    {
        CandidatePackageFeed candidate = Candidate;
        AssertAdopterRuntimeFixtures(candidate, ["migration", "aspnet-host"]);

        var scenarios = new List<CheckpointScenarioResult>
        {
            AssertPublicApiSnapshotWorkflow(candidate),
        };
        AssertCacheLifecycleOracle(candidate);
        scenarios.Add(candidate.AssertMissingSharedFrameworkDiagnostic());
        scenarios.Add(Passed("cache-miss-population-hit"));
        scenarios.Add(Passed("cache-corruption-recompute"));
        candidate.WriteShardEvidence("adopter-runtime-extended", scenarios);
    }

    [Test]
    public void PackedCandidate_ConsumerCleanupPolicyExecution()
    {
        CandidatePackageFeed candidate = Candidate;
        IReadOnlyList<CheckpointScenarioResult> scenarios =
            AssertConsumerCleanupPolicyExecution(candidate);
        candidate.WriteShardEvidence("consumer-cleanup-policy-execution", scenarios);
    }

    [Test]
    public void PackedCandidate_ConsumerCleanupDependencyContractIdParity()
    {
        CandidatePackageFeed candidate = Candidate;
        IReadOnlyList<CheckpointScenarioResult> scenarios =
            AssertConsumerCleanupDependencyContractIdParity(candidate);
        candidate.WriteShardEvidence("consumer-cleanup-dependency-contract-id-parity", scenarios);
    }

    [Test]
    public void PackedCandidate_ConsumerCleanupLayerOverlapAndPolicyShape()
    {
        CandidatePackageFeed candidate = Candidate;
        IReadOnlyList<CheckpointScenarioResult> scenarios =
            AssertConsumerCleanupLayerOverlapAndPolicyShape(candidate, out ConsumerPolicyShape policyShape);
        candidate.WriteShardEvidence("consumer-cleanup-layer-overlap-and-policy-shape", scenarios, policyShape);
    }

    [Test]
    public void PackedCandidate_ConsumerCleanupConfigurationAndIdentity()
    {
        CandidatePackageFeed candidate = Candidate;
        IReadOnlyList<CheckpointScenarioResult> scenarios =
            AssertConsumerCleanupConfigurationAndIdentity(candidate);
        candidate.WriteShardEvidence("consumer-cleanup-configuration-and-identity", scenarios);
    }

    [Test]
    public void PackedCandidate_ConsumerCleanupSourceSetAuthoring()
    {
        CandidatePackageFeed candidate = Candidate;
        IReadOnlyList<CheckpointScenarioResult> scenarios =
            AssertConsumerCleanupSourceSetAuthoring(candidate);
        candidate.WriteShardEvidence("consumer-cleanup-source-set-authoring", scenarios);
    }

    [Test]
    public void PackedCandidate_PublicApiSurfaceSelectorSnapshotAndRole()
    {
        CandidatePackageFeed candidate = Candidate;
        using AdoptionAcceptanceFixture fixture = CreatePublicApiSurfaceSelectorFixture();
        candidate.WriteShardEvidence("public-api-surface-selector-snapshot-and-role",
            [AssertSurfaceSelectorSnapshotReduction(candidate, fixture), AssertSurfaceSelectorRolePreservation(candidate, fixture)]);
    }

    [Test]
    public void PackedCandidate_PublicApiSurfaceSelectorDeltaAndMembership()
    {
        CandidatePackageFeed candidate = Candidate;
        using AdoptionAcceptanceFixture fixture = CreatePublicApiSurfaceSelectorFixture();

        // The lifecycle scenarios consume the initial reviewed snapshots. They re-establish that
        // fixture-local precondition without emitting the snapshot-reduction scenario a second time.
        _ = AssertSurfaceSelectorSnapshotReduction(candidate, fixture);
        candidate.WriteShardEvidence("public-api-surface-selector-delta-and-membership",
        [
            AssertSurfaceSelectorExactDeltaLifecycle(candidate, fixture),
            AssertSurfaceSelectorMembershipReviewVisibility(candidate, fixture),
        ]);
    }

    [Test]
    public void PackedCandidate_PublicApiSurfaceSelectorEnforcement()
    {
        CandidatePackageFeed candidate = Candidate;
        using AdoptionAcceptanceFixture fixture = CreatePublicApiSurfaceSelectorFixture();

        // Strict validation and Testing-adapter parity require fresh reviewed snapshots but do
        // not independently claim the snapshot-reduction evidence scenario.
        _ = AssertSurfaceSelectorSnapshotReduction(candidate, fixture);
        _ = AssertSurfaceSelectorExactDeltaLifecycle(candidate, fixture);
        candidate.WriteShardEvidence("public-api-surface-selector-enforcement",
        [
            AssertSurfaceSelectorEscapeFailsClosed(candidate, fixture),
            AssertSurfaceSelectorStrictRunIsGreen(candidate, fixture),
            candidate.AssertPublicApiSurfaceSelectorTestingParity(fixture),
        ]);
    }

    private CandidatePackageFeed Candidate => _candidate
        ?? throw new InvalidOperationException("Checkpoint B candidate was not prepared.");

    private static CheckpointScenarioResult Passed(string id) => new(id, "passed", null);

    private static void AssertAdopterRuntimeFixtures(CandidatePackageFeed candidate, IEnumerable<string> fixtureIds)
    {
        foreach (string fixtureId in fixtureIds)
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
            string profilePath = Path.Combine(fixture.Root, "checkpoint-b-profile.json");
            CommandResult profiledDefault = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--ensure-built",
                "--profile", profilePath);

            Assert.Multiple(() =>
            {
                Assert.That(profiledDefault.ExitCode, Is.EqualTo(sequential.ExitCode), fixtureId);
                Assert.That(CanonicalJson(profiledDefault.StandardOutput),
                    Is.EqualTo(CanonicalJson(sequential.StandardOutput)), fixtureId);
                Assert.That(File.Exists(profilePath), Is.True, profilePath);
                Assert.That(sequential.StandardError, Does.Not.Contain("\u001b["), fixtureId);
            });
        }
    }

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

    private static CommandResult Run(ProcessStartInfo startInfo) =>
        Run(startInfo, TestContext.CurrentContext.CancellationToken);

    internal static CommandResult Run(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{startInfo.FileName}'.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        try
        {
            process.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
            Task.WaitAll(standardOutput, standardError);
            throw;
        }

        Task.WaitAll(standardOutput, standardError);
        return new CommandResult(process.ExitCode, standardOutput.Result, standardError.Result);
    }

    internal sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => $"stdout:{Environment.NewLine}{StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{StandardError}";
    }

    private sealed record PackagePairEvidence(
        string Id,
        string Version,
        PackageSubjectEvidence Package,
        PackageSubjectEvidence Symbols);

    private sealed record PackageSubjectEvidence(string Kind, string File, long Size, string Sha256);

    private sealed record CheckpointScenarioResult(string Id, string Result, string? Reason);
}
