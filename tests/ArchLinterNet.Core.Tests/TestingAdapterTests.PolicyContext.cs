using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class TestingAdapterTests
{
    [Test]
    public void EvaluateDebtGate_AcceptsCliShapedSchemaFiveContexts()
    {
        string contractPath = WriteSelfForbiddenPolicy();
        string baselinePath = Path.Combine(_tempDir, "baseline.yml");
        File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");

        string baseContextPath = WritePolicyContext("base.context.json", "strict");
        string currentContextPath = WritePolicyContext("current.context.json", "audit");

        ArchitectureDebtGateOutcome outcome = ArchitectureAssertions.FromPolicy(contractPath)
            .WithContracts("harmless")
            .WithBaseline(baselinePath)
            .WithPolicyWeakeningContexts(baseContextPath, currentContextPath)
            .EvaluateDebtGate("strict");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.PolicyWeakeningRequested, Is.True);
            Assert.That(outcome.PolicyWeakening?.HasErrors, Is.True);
            Assert.That(outcome.Passed, Is.False);
        });
    }

    [Test]
    public void EvaluateDebtGate_IdenticalCliShapedContextsReportNoWeakening()
    {
        string contractPath = WriteSelfForbiddenPolicy();
        string baselinePath = Path.Combine(_tempDir, "baseline.yml");
        File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");
        string contextPath = WritePolicyContext("unchanged.context.json", "strict");

        ArchitectureDebtGateOutcome outcome = ArchitectureAssertions.FromPolicy(contractPath)
            .WithContracts("harmless")
            .WithBaseline(baselinePath)
            .WithPolicyWeakeningContexts(contextPath, contextPath)
            .EvaluateDebtGate("strict");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.PolicyWeakeningRequested, Is.True);
            Assert.That(outcome.PolicyWeakening?.HasErrors, Is.False);
            Assert.That(outcome.Passed, Is.True);
        });
    }

    [Test]
    public void EvaluateDebtGate_InvalidContextFailsClosedBeforeComparison()
    {
        string contractPath = WriteSelfForbiddenPolicy();
        string baselinePath = Path.Combine(_tempDir, "baseline.yml");
        File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");
        string invalidContextPath = Path.Combine(_tempDir, "invalid.context.json");
        File.WriteAllText(invalidContextPath, "{}");

        Assert.That(
            () => ArchitectureAssertions.FromPolicy(contractPath)
                .WithContracts("harmless")
                .WithBaseline(baselinePath)
                .WithPolicyWeakeningContexts(invalidContextPath, invalidContextPath)
                .EvaluateDebtGate("strict"),
            Throws.ArgumentException);
    }

    private string WritePolicyContext(string fileName, string mode)
    {
        ArchitecturePolicyContextExport context = new(
            ArchitecturePolicyContextExport.CurrentSchemaVersion,
            "architecture-policy-context",
            new ArchitecturePolicyContextPolicy("Builder Test", 1, "architecture/dependencies.arch.yml", false),
            new ArchitecturePolicyContextGuardrails("error"),
            new ArchitecturePolicyContextAnalysis([], [], [], [], []),
            [new ArchitecturePolicyContextSource("architecture/dependencies.arch.yml", "root", 0, null, null, [])],
            [],
            [new ArchitecturePolicyContextContract(mode, "dependency", "harmless", "harmless", null, null, [], [], [], [], [], [], null)],
            [],
            [],
            [],
            [],
            [],
            [],
            [])
        {
            WaiverLifecycleProfile = "compatibility",
        };

        string path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, ArchitecturePolicyContextFormatter.FormatAsJson(context));
        return path;
    }
}
