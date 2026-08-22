using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Core.Validation.Abstractions;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDebtGateApplicationServiceTests
{
    [Test]
    public void Evaluate_ErrorSeverityWeakeningFailsGateWhileMatchedDebtRemainsSeparate()
    {
        ArchitectureBaselineComparisonEntry matched = Entry("Sample.Application.Service", "Sample.Infrastructure.Repository");
        var baseline = new StubBaselineService
        {
            Outcome = new BaselineVerifyOutcome(true, true, [], [matched], [], [], [])
            {
                Entries = [new BaselineLifecycleEntry(matched, BaselineEntryLifecycle.Matched)],
            },
        };
        ArchitectureDebtGateOutcome result = new ArchitectureDebtGateApplicationService(baseline).Evaluate(new ArchitectureDebtGateRequest
        {
            PolicyPath = "policy.yml",
            BaselinePath = "baseline.yml",
            BasePolicyContext = Context("strict"),
            CurrentPolicyContext = Context("audit"),
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Passed, Is.False);
            Assert.That(result.PersistentDebt.Frozen, Is.EqualTo(new[] { matched }));
            Assert.That(result.PolicyWeakening!.Findings.Single().Kind, Is.EqualTo("strict_to_audit"));
            Assert.That(result.PolicyWeakening.Findings.Single().Severity, Is.EqualTo("error"));
        });
    }

    [Test]
    public void Evaluate_UnpairedPolicyContextFailsBeforeBaselineComparison()
    {
        var baseline = new StubBaselineService();
        var request = new ArchitectureDebtGateRequest
        {
            PolicyPath = "policy.yml",
            BaselinePath = "baseline.yml",
            BasePolicyContext = Context("strict"),
        };

        Assert.Multiple(() =>
        {
            Assert.That(() => new ArchitectureDebtGateApplicationService(baseline).Evaluate(request), Throws.ArgumentException);
            Assert.That(baseline.VerifyCalls, Is.Zero);
        });
    }

    [Test]
    public void Formatters_PreservePersistentAndWeakeningTypedSections()
    {
        var identity = new ArchitectureViolationIdentity(
            1,
            "dependency",
            "reference",
            "dependency",
            "Sample.Application",
            "Sample.Application.Service",
            "Run",
            "Sample.Infrastructure",
            "Sample.Infrastructure.Repository",
            "Save",
            2,
            "condition-set");
        var entry = new ArchitectureBaselineComparisonEntry(
            "strict",
            "dependency",
            "Sample.Application.Service",
            "Sample.Infrastructure.Repository",
            "reviewed",
            identity);
        ArchitectureDebtGateOutcome outcome = new(
            true,
            false,
            new ArchitectureDebtGateEvaluation(true, "all", []),
            new BaselineVerifyOutcome(true, false, [entry], [], [], [], [])
            {
                Entries = [new BaselineLifecycleEntry(entry, BaselineEntryLifecycle.New)],
            })
        {
            PolicyWeakening = new ArchitectureDebtGateApplicationService(new StubBaselineService
            {
                Outcome = new BaselineVerifyOutcome(true, true, [], [], [], [], []),
            }).Evaluate(new ArchitectureDebtGateRequest
            {
                PolicyPath = "policy.yml",
                BaselinePath = "baseline.yml",
                BasePolicyContext = Context("strict", "warn"),
                CurrentPolicyContext = Context("audit", "warn"),
            }).PolicyWeakening,
            PolicyWeakeningRequested = true,
        };

        using JsonDocument json = JsonDocument.Parse(ArchitectureDebtGateFormatter.FormatAsJson(outcome));
        using JsonDocument sarif = JsonDocument.Parse(ArchitectureDebtGateFormatter.FormatAsSarif(outcome, "1.0.0"));
        JsonElement persistentProperties = sarif.RootElement.GetProperty("runs")[0].GetProperty("results")
            .EnumerateArray()
            .Single(result => result.GetProperty("properties").GetProperty("gate_section").GetString() == "persistent_debt")
            .GetProperty("properties");
        JsonElement normalizedFinding = persistentProperties.GetProperty("arch_linter_net");
        using JsonDocument canonicalIdentity = JsonDocument.Parse(persistentProperties.GetProperty("canonical_identity").GetString()!);

        Assert.Multiple(() =>
        {
            Assert.That(json.RootElement.GetProperty("persistent_debt").GetProperty("entries")[0]
                .GetProperty("status").GetString(), Is.EqualTo("new"));
            Assert.That(json.RootElement.GetProperty("policy_weakening").GetProperty("findings")[0]
                .GetProperty("classification").GetString(), Is.EqualTo("semantic"));
            Assert.That(sarif.RootElement.GetProperty("runs")[0].GetProperty("results")
                .EnumerateArray().Select(result => result.GetProperty("properties").GetProperty("gate_section").GetString()),
                Is.EquivalentTo(new[] { "persistent_debt", "policy_weakening" }));
            Assert.That(persistentProperties.GetProperty("identity_version").GetInt32(), Is.EqualTo(1));
            Assert.That(persistentProperties.GetProperty("source_assembly").GetString(), Is.EqualTo("Sample.Application"));
            Assert.That(persistentProperties.GetProperty("target_assembly").GetString(), Is.EqualTo("Sample.Infrastructure"));
            Assert.That(persistentProperties.GetProperty("occurrence").GetInt32(), Is.EqualTo(2));
            Assert.That(canonicalIdentity.RootElement.GetProperty("occurrence").GetInt32(), Is.EqualTo(2));
            Assert.That(normalizedFinding.GetProperty("canonical_identity").GetString(),
                Is.EqualTo(persistentProperties.GetProperty("canonical_identity").GetString()));
        });
    }

    private static ArchitectureBaselineComparisonEntry Entry(string source, string target) => new(
        "strict", "dependency", source, target, "reviewed",
        new ArchitectureViolationIdentity(1, "dependency", "reference", "dependency", "Sample", source, null, "Sample", target, null, 0));

    private static ArchitecturePolicyContextExport Context(string mode, string severity = "error") => new(
        ArchitecturePolicyContextExport.CurrentSchemaVersion,
        "architecture-policy-context",
        new ArchitecturePolicyContextPolicy("Sample policy", 1, "policy.yml", false),
        new ArchitecturePolicyContextGuardrails(severity),
        new ArchitecturePolicyContextAnalysis([], [], [], [], []),
        [new ArchitecturePolicyContextSource("policy.yml", "root", 0, null, null, [])],
        [],
        [new ArchitecturePolicyContextContract(mode, "dependency", "boundary", "boundary", null, null, [], [], [], [], [], [], null)],
        [], [], [], [], [], [], []);

    private sealed class StubBaselineService : IArchitectureBaselineApplicationService
    {
        public BaselineVerifyOutcome Outcome { get; init; } = new(true, true, [], [], [], [], []);

        public int VerifyCalls { get; private set; }

        public BaselineGenerationOutcome Generate(BaselineGenerationRequest request) => throw new NotSupportedException();

        public BaselineUpdateOutcome Update(BaselineUpdateRequest request) => throw new NotSupportedException();

        public BaselinePruneOutcome Prune(BaselinePruneRequest request) => throw new NotSupportedException();

        public BaselineDiffOutcome Diff(BaselineDiffRequest request) => throw new NotSupportedException();

        public BaselineVerifyOutcome Verify(BaselineVerifyRequest request)
        {
            VerifyCalls++;
            return Outcome;
        }

        public BaselineMigrateOutcome Migrate(BaselineMigrateRequest request) => throw new NotSupportedException();
    }
}
