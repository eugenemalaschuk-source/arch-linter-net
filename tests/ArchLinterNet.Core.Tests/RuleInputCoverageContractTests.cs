using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class RuleInputCoverageContractTests
{
    private const string FixtureRoot = "ArchLinterNet.Core.Tests.RuleInputCoverageFixtures";

    private static readonly Assembly[] _targetAssemblies = { typeof(RuleInputCoverageContractTests).Assembly };

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            repositoryRoot: AppContext.BaseDirectory,
            targetAssemblies: _targetAssemblies,
            missingAssemblyNames: Array.Empty<string>(),
            assemblyProbingPaths: Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateDocument()
    {
        ArchitectureContractDocument document = new();

        document.Layers["audio"] = new ArchitectureLayer { Namespace = $"{FixtureRoot}.Audio" };
        document.Layers["video"] = new ArchitectureLayer { Namespace = $"{FixtureRoot}.Video" };
        document.Layers["ghost"] = new ArchitectureLayer { Namespace = $"{FixtureRoot}.Ghost" };

        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Name = "audio-rule",
            Id = "audio-rule",
            Source = "audio",
            Forbidden = { "video" },
            Reason = "Audio must not depend on video."
        });

        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Name = "video-to-ghost-rule",
            Id = "video-to-ghost-rule",
            Source = "video",
            Forbidden = { "ghost" },
            Reason = "Video must not depend on ghost."
        });

        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Name = "typo-rule",
            Id = "typo-rule",
            Source = "does_not_exist_layer",
            Forbidden = { "audio" },
            Reason = "Placeholder rule with a dangling source layer."
        });

        return document;
    }

    private static ArchitectureCoverageContract CreateRuleInputContract(
        IEnumerable<string> contractIds, IEnumerable<ArchitectureCoverageExclusion>? exclude = null)
    {
        ArchitectureCoverageContract contract = new()
        {
            Name = "rule-input-coverage",
            Id = "rule-input-coverage",
            Scope = "rule_input",
            Reason = "Flag if referenced rules stop matching any code.",
        };

        contract.ContractIds.AddRange(contractIds);

        if (exclude != null)
        {
            contract.Exclude.AddRange(exclude);
        }

        return contract;
    }

    [Test]
    public void CheckRuleInputCoverage_ContractWithRealMatches_ProducesNoFindings()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(
            CreateRuleInputContract(new[] { "audio-rule" }));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void CheckRuleInputCoverage_TargetLayerWithNoMatchingCode_IsReportedAsEmptyInput()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(
            CreateRuleInputContract(new[] { "video-to-ghost-rule" }));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].SourceType, Is.EqualTo("video-to-ghost-rule"));
        Assert.That(findings[0].ForbiddenNamespace, Is.EqualTo("empty-input"));
        Assert.That(findings[0].ForbiddenReferences, Is.EqualTo(new[] { "ghost" }));
    }

    [Test]
    public void CheckRuleInputCoverage_DanglingLayerReference_IsReportedAsUnresolved()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(
            CreateRuleInputContract(new[] { "typo-rule" }));

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].SourceType, Is.EqualTo("typo-rule"));
        Assert.That(findings[0].ForbiddenNamespace, Is.EqualTo("unresolved"));
        Assert.That(findings[0].ForbiddenReferences, Is.EqualTo(new[] { "does_not_exist_layer" }));
    }

    [Test]
    public void CheckRuleInputCoverage_ExcludedContractId_ProducesNoFindings()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        List<ArchitectureViolation> findings = runner.CheckCoverageContract(
            CreateRuleInputContract(
                new[] { "video-to-ghost-rule", "typo-rule" },
                new[]
                {
                    new ArchitectureCoverageExclusion
                    {
                        ContractId = "video-to-ghost-rule",
                        Reason = "Ghost layer is intentionally unused for now."
                    },
                    new ArchitectureCoverageExclusion
                    {
                        ContractId = "typo-rule",
                        Reason = "Placeholder rule retained for documentation purposes."
                    }
                }));

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void CheckRuleInputCoverage_RepeatedRuns_AreDeterministic()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureCoverageContract contract = CreateRuleInputContract(
            new[] { "audio-rule", "video-to-ghost-rule", "typo-rule" });

        ArchitectureContractRunner firstRunner = new(CreateContext(), document);
        ArchitectureContractRunner secondRunner = new(CreateContext(), document);

        List<ArchitectureViolation> first = firstRunner.CheckCoverageContract(contract);
        List<ArchitectureViolation> second = secondRunner.CheckCoverageContract(contract);

        Assert.That(
            first.Select(f => (f.SourceType, f.ForbiddenNamespace, Reference: f.ForbiddenReferences.Single())),
            Is.EqualTo(second.Select(f => (f.SourceType, f.ForbiddenNamespace, Reference: f.ForbiddenReferences.Single()))));
    }
}
