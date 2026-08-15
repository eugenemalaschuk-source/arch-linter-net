using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private const string ModularConsumerFixtureId = "modular-consumer";

    /// <summary>
    /// Consumer-cleanup scenarios whose failure is a known, separately tracked product defect
    /// rather than an unexplained regression. A registered scenario is still recorded as
    /// <c>failed</c> in the platform evidence, and the release-evidence aggregator refuses to
    /// authorize publication while any scenario is failed. The registry only keeps the executable
    /// gate honest in both directions: a NEW failure fails this test immediately, and a tracked
    /// defect that has silently been fixed also fails it so the entry gets removed.
    /// </summary>
    private static readonly Dictionary<string, string> _trackedConsumerCleanupDefects =
        new(StringComparer.Ordinal);

    private static List<CheckpointScenarioResult> AssertConsumerCleanupPolicyExecution(
        CandidatePackageFeed candidate)
    {
        using PreparedConsumerCleanup prepared = PrepareConsumerCleanup(candidate);
        AdoptionAcceptanceFixture consumer = prepared.Consumer;

        return
        [
            AssertComposedPolicyAssemblyFreeCheck(candidate, consumer),
            AssertNonDestructiveBuildPreparation(candidate, consumer),
            candidate.AssertRepeatedTestingEnsureBuilt(),
            AssertStrictCyclesBaselineScope(candidate, consumer),
        ];
    }

    private static List<CheckpointScenarioResult> AssertConsumerCleanupPolicyContractsAndShape(
        CandidatePackageFeed candidate,
        out ConsumerPolicyShape policyShape)
    {
        using PreparedConsumerCleanup prepared = PrepareConsumerCleanup(candidate);
        AdoptionAcceptanceFixture consumer = prepared.Consumer;

        var scenarios = new List<CheckpointScenarioResult>
        {
            AssertDependencyContractIdParity(candidate, consumer),
            AssertLayerOverlapAllowance(candidate, consumer),
        };

        policyShape = DescribeConsumerPolicyShape(consumer, prepared.Expansion);
        scenarios.Add(AssertConsumerPolicyShape(policyShape));
        return scenarios;
    }

    private static List<CheckpointScenarioResult> AssertConsumerCleanupConfigurationAndIdentity(
        CandidatePackageFeed candidate)
    {
        using PreparedConsumerCleanup prepared = PrepareConsumerCleanup(candidate);
        return
        [
            AssertActionableSchemaDiagnostics(candidate),
            AssertNamespaceAllowancePattern(candidate),
            AssertJsonConfigurationErrorFormat(candidate, prepared.Consumer),
            candidate.AssertReleaseIdentityConsistency(),
        ];
    }

    private static List<CheckpointScenarioResult> AssertConsumerCleanupSourceSetAuthoring(
        CandidatePackageFeed candidate)
    {
        using PreparedConsumerCleanup prepared = PrepareConsumerCleanup(candidate);
        return
        [
            AssertSourceSetAssemblyAuthoring(prepared.Expansion),
            AssertDiscoveredProjectSetAuthoring(prepared.Expansion),
            AssertSourceSetEnrolment(candidate),
            AssertStaleSourceSelectorFailsClosed(candidate),
        ];
    }

    private static PreparedConsumerCleanup PrepareConsumerCleanup(CandidatePackageFeed candidate)
    {
        var consumer = AdoptionAcceptanceFixture.Create(ModularConsumerFixtureId);
        try
        {
            consumer.Build();
            CommandResult strict = candidate.RunTool(consumer.Root,
                "--policy", consumer.PolicyPath, "--strict", "--format", "json", "--ensure-built");
            Assert.That(strict.ExitCode, Is.EqualTo(0),
                $"The synthetic modular consumer must validate cleanly.{Environment.NewLine}{strict.CombinedOutput}");
            return new PreparedConsumerCleanup(consumer, JsonDocument.Parse(strict.StandardOutput));
        }
        catch
        {
            consumer.Dispose();
            throw;
        }
    }

    private sealed class PreparedConsumerCleanup(
        AdoptionAcceptanceFixture consumer,
        JsonDocument strictReport) : IDisposable
    {
        public AdoptionAcceptanceFixture Consumer => consumer;

        public JsonElement Expansion => strictReport.RootElement.GetProperty("source_set_expansion");

        public void Dispose()
        {
            strictReport.Dispose();
            consumer.Dispose();
        }
    }

    /// <summary>
    /// Records a consumer-cleanup verdict, honoring <see cref="_trackedConsumerCleanupDefects"/>.
    /// </summary>
    private static CheckpointScenarioResult Verdict(string id, bool satisfied, string evidence)
    {
        if (_trackedConsumerCleanupDefects.TryGetValue(id, out string? tracked))
        {
            Assert.That(satisfied, Is.False,
                $"Scenario '{id}' is registered as a tracked release-blocking defect but now satisfies "
                + $"its contract. Remove the registry entry so it gates the release again."
                + $"{Environment.NewLine}{evidence}");
            return new CheckpointScenarioResult(id, "failed", tracked);
        }

        Assert.That(satisfied, Is.True, evidence);
        return Passed(id);
    }

    // F1 — a composed policy built from imported fragments loads and validates, and the same
    // authored policy passes `policy check` without any built assembly. The 0.6.0 workaround was
    // collapsing every fragment back into one monolithic policy file.
    private static CheckpointScenarioResult AssertComposedPolicyAssemblyFreeCheck(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture consumer)
    {
        using AdoptionAcceptanceFixture cleanCheckout = AdoptionAcceptanceFixture.Create(ModularConsumerFixtureId);
        CommandResult check = candidate.RunTool(cleanCheckout.Root,
            "policy", "check", "--policy", cleanCheckout.PolicyPath, "--format", "json");

        using JsonDocument document = JsonDocument.Parse(check.StandardOutput);
        JsonElement root = document.RootElement;
        string[] deferredContracts = root.GetProperty("deferred_checks").EnumerateArray()
            .Select(entry => entry.GetProperty("contract_id").GetString() ?? string.Empty)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(check.ExitCode, Is.EqualTo(0), check.CombinedOutput);
            Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("valid-with-deferred-checks"));
            Assert.That(root.GetProperty("completed_checks").EnumerateArray()
                .Select(entry => entry.GetString()), Does.Contain("imports-and-composition"));
            Assert.That(deferredContracts, Does.Contain("modules-never-reference-the-host/synthetic-modules-m01"),
                "Assembly-free validation must still report the expanded per-source instances.");
            Assert.That(Directory.GetDirectories(cleanCheckout.Root, "bin", SearchOption.AllDirectories), Is.Empty,
                "`policy check` must not build the consumer.");
            Assert.That(File.ReadAllText(consumer.PolicyPath), Does.Contain("imports:"),
                "The consumer policy must stay composed rather than collapse into a monolith.");
        });
        return Passed("composed-policy-assembly-free-check");
    }

    // F2, CLI half — repeated build preparation preserves the verified build outputs, so a consumer
    // needs no post-analysis rebuild shim before packing or testing. The oracle covers every
    // selected primary output, not just assemblies: a torn PDB breaks a consumer's downstream
    // `dotnet test --no-build` just as badly. The packaged Testing half of #436 is proven
    // separately by `packaged-testing-ensure-built`.
    private static CheckpointScenarioResult AssertNonDestructiveBuildPreparation(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture consumer)
    {
        Dictionary<string, string> before = HashBuildOutputs(consumer.Root);
        Assert.That(before, Is.Not.Empty, "The consumer fixture must have build outputs to preserve.");
        Assert.That(before.Keys, Has.Some.EndsWith(".pdb"),
            "The preservation oracle must cover symbols, not only assemblies.");

        CommandResult second = candidate.RunTool(consumer.Root,
            "--policy", consumer.PolicyPath, "--strict", "--format", "json", "--ensure-built");
        Dictionary<string, string> after = HashBuildOutputs(consumer.Root);

        Assert.Multiple(() =>
        {
            Assert.That(second.ExitCode, Is.EqualTo(0), second.CombinedOutput);
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys),
                "Repeated build preparation must not delete a verified build output.");
            Assert.That(after, Is.EqualTo(before),
                "Repeated build preparation must not rewrite a verified build output.");
        });
        return Passed("non-destructive-ensure-built");
    }

    // F4 — whole-graph strict cycle baselines contain only real cycle edges. The probe policy
    // declares one genuinely cyclic contract and one ordinary acyclic inter-layer contract.
    private static CheckpointScenarioResult AssertStrictCyclesBaselineScope(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture consumer)
    {
        string policy = Path.Combine(consumer.Root, "dependencies.cycles.arch.yml");
        string empty = Path.Combine(consumer.Root, "cycles-empty-baseline.yml");
        string updated = Path.Combine(consumer.Root, "cycles-updated-baseline.yml");
        File.WriteAllText(empty, $"version: 2{Environment.NewLine}baseline: {{}}{Environment.NewLine}");

        CommandResult update = candidate.RunTool(consumer.Root,
            "baseline", "update", "--config", policy, "--baseline", empty, "--output", updated);
        CommandResult drifted = candidate.RunTool(consumer.Root,
            "baseline", "verify", "--config", policy, "--baseline", empty, "--json");
        CommandResult synchronized = candidate.RunTool(consumer.Root,
            "baseline", "verify", "--config", policy, "--baseline", updated, "--json");

        string persisted = File.ReadAllText(updated);
        using JsonDocument drift = JsonDocument.Parse(drifted.StandardOutput);
        using JsonDocument sync = JsonDocument.Parse(synchronized.StandardOutput);

        Assert.Multiple(() =>
        {
            Assert.That(update.ExitCode, Is.EqualTo(0), update.CombinedOutput);
            Assert.That(persisted, Does.Contain("composition-internals-cycle-probe"));
            Assert.That(persisted, Does.Not.Contain("host-and-abstractions-cycle-probe"),
                "An ordinary acyclic inter-layer edge must never become accepted cycle debt.");
            Assert.That(drifted.ExitCode, Is.EqualTo(1), drifted.CombinedOutput);
            Assert.That(drift.RootElement.GetProperty("inSync").GetBoolean(), Is.False);
            Assert.That(drift.RootElement.GetProperty("counts").GetProperty("new").GetInt32(), Is.EqualTo(2));
            Assert.That(synchronized.ExitCode, Is.EqualTo(0), synchronized.CombinedOutput);
            Assert.That(sync.RootElement.GetProperty("inSync").GetBoolean(), Is.True);
            Assert.That(sync.RootElement.GetProperty("counts").GetProperty("new").GetInt32(), Is.Zero);
        });
        return Passed("strict-cycles-baseline-scope");
    }

    // F5 — `id` on dependency contracts is accepted identically by the packaged schema (through
    // `policy check`) and by the runtime, including after import composition, and the authored id
    // still selects every instance an expanded contract produced.
    private static CheckpointScenarioResult AssertDependencyContractIdParity(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture consumer)
    {
        CommandResult check = candidate.RunTool(consumer.Root,
            "policy", "check", "--policy", consumer.PolicyPath, "--format", "json");
        CommandResult selected = candidate.RunTool(consumer.Root,
            "--policy", consumer.PolicyPath, "--strict", "--format", "json", "--ensure-built",
            "--contract", "modules-never-reference-the-host");

        using JsonDocument checkDocument = JsonDocument.Parse(check.StandardOutput);
        string[] deferredContracts = checkDocument.RootElement.GetProperty("deferred_checks").EnumerateArray()
            .Select(entry => entry.GetProperty("contract_id").GetString() ?? string.Empty)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(check.ExitCode, Is.EqualTo(0), check.CombinedOutput);
            Assert.That(deferredContracts, Does.Contain("modules-do-not-depend-on-the-host"),
                "The packaged schema must accept `id` on a dependency contract authored in a fragment.");
            Assert.That(selected.ExitCode, Is.EqualTo(0), selected.CombinedOutput);
        });
        return Passed("dependency-contract-id-parity");
    }

    // F8 — an intentional, non-containment layer overlap is reconciled locally with
    // `overlaps_with`; removing the declaration restores the policy-consistency finding, so the
    // allowance is a real allowance and not a globally relaxed consistency setting.
    private static CheckpointScenarioResult AssertLayerOverlapAllowance(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture consumer)
    {
        CommandResult declared = candidate.RunTool(consumer.Root,
            "--policy", consumer.PolicyPath, "--strict", "--format", "json", "--ensure-built");

        string layers = Path.Combine(consumer.Root, "fragments", "layers.yml");
        string original = File.ReadAllText(layers);
        CommandResult withdrawn;
        try
        {
            File.WriteAllText(layers, original.Replace(
                $"    overlaps_with: [modules]{Environment.NewLine}", string.Empty, StringComparison.Ordinal));
            withdrawn = candidate.RunTool(consumer.Root,
                "--policy", consumer.PolicyPath, "--strict", "--format", "json", "--ensure-built");
        }
        finally
        {
            File.WriteAllText(layers, original);
        }

        using JsonDocument accepted = JsonDocument.Parse(declared.StandardOutput);
        Assert.Multiple(() =>
        {
            Assert.That(original, Does.Contain("overlaps_with: [modules]"),
                "The fixture must declare the intentional overlap it is meant to prove.");
            Assert.That(declared.ExitCode, Is.EqualTo(0), declared.CombinedOutput);
            Assert.That(accepted.RootElement.GetProperty("policy_consistency_findings").GetArrayLength(), Is.Zero);
            Assert.That(withdrawn.ExitCode, Is.EqualTo(1), withdrawn.CombinedOutput);
            Assert.That(withdrawn.StandardOutput, Does.Contain("layer-overlap"));
        });
        return Passed("layer-overlap-allowance");
    }

    /// <summary>
    /// Every selected primary build output, by content. Assemblies alone are not the invariant:
    /// #436 corrupted whatever the post-build evaluation touched, and a consumer continuing with
    /// `dotnet test --no-build` or packing needs its symbols and deps/runtime config intact too.
    /// </summary>
    private static Dictionary<string, string> HashBuildOutputs(string root)
    {
        string[] primaryOutputs = ["*.dll", "*.pdb", "*.deps.json", "*.runtimeconfig.json"];
        return primaryOutputs
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparer.Ordinal);
    }
}
