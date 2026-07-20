using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureCoverageBaselineTests
{
    private static readonly ArchitectureBaselineGenerator _generator = new();
    private static readonly ArchitectureBaselineLoadingService _loadingService = new();

    private const string NamespaceFixtureRoot = "ArchLinterNet.Core.Tests.NamespaceCoverageFixtures.Features";
    private const string RuleInputFixtureRoot = "ArchLinterNet.Core.Tests.RuleInputCoverageFixtures";

    private static readonly Assembly[] _targetAssemblies = { typeof(ArchitectureCoverageBaselineTests).Assembly };

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private static ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            repositoryRoot: AppContext.BaseDirectory,
            targetAssemblies: _targetAssemblies,
            missingAssemblyNames: Array.Empty<string>(),
            assemblyProbingPaths: Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateNamespaceScopeDocument()
    {
        ArchitectureContractDocument document = new();

        // "AlphaGap" and "ZetaGap" namespaces have no covering layer, so they are reported as
        // uncovered; only "AlphaGap" is declared covered for the strict dependency contract below.
        document.Layers["covered"] = new ArchitectureLayer { Namespace = $"{NamespaceFixtureRoot}.Audio" };

        document.Contracts.StrictCoverage.Add(new ArchitectureCoverageContract
        {
            Name = "feature-namespace-coverage",
            Id = "feature-namespace-coverage",
            Scope = "namespace",
            Roots = { new ArchitectureCoverageRoot { Namespace = NamespaceFixtureRoot } },
            Reason = "Every feature namespace must be covered by a layer."
        });

        return document;
    }

    private static ArchitectureContractDocument CreateRuleInputScopeDocument()
    {
        ArchitectureContractDocument document = new();

        document.Layers["audio"] = new ArchitectureLayer { Namespace = $"{RuleInputFixtureRoot}.Audio" };
        document.Layers["video"] = new ArchitectureLayer { Namespace = $"{RuleInputFixtureRoot}.Video" };
        document.Layers["ghost"] = new ArchitectureLayer { Namespace = $"{RuleInputFixtureRoot}.Ghost" };

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

        document.Contracts.StrictCoverage.Add(new ArchitectureCoverageContract
        {
            Name = "rule-input-coverage",
            Id = "rule-input-coverage",
            Scope = "rule_input",
            ContractIds = { "video-to-ghost-rule", "typo-rule" },
            Reason = "Flag if referenced rules stop matching any code."
        });

        return document;
    }

    [Test]
    public void InitialBaselineGeneration_CapturesUncoveredNamespace_UnderStrictCoverageGroup()
    {
        ArchitectureContractDocument document = CreateNamespaceScopeDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        runner.CheckCoverageContract(document.Contracts.StrictCoverage[0]);

        ArchitectureBaselineDocument baseline = _generator.Generate(
            document, runner.BaselineCandidates, "generated baseline");

        Assert.That(baseline.Baseline.StrictCoverage, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictCoverage[0].Id, Is.EqualTo("feature-namespace-coverage"));
        Assert.That(baseline.Baseline.StrictCoverage[0].IgnoredViolations,
            Has.Some.Matches<ArchitectureBaselineIgnoredViolation>(v =>
                v.SourceType.EndsWith("AlphaGap", StringComparison.Ordinal)
                && v.ForbiddenReference == "uncovered namespace"));
    }

    [Test]
    public void InitialBaselineGeneration_CapturesRuleInputFindings_UnderStrictCoverageGroup()
    {
        ArchitectureContractDocument document = CreateRuleInputScopeDocument();
        ArchitectureContractRunner runner = new(CreateContext(), document);

        runner.CheckCoverageContract(document.Contracts.StrictCoverage[0]);

        ArchitectureBaselineDocument baseline = _generator.Generate(
            document, runner.BaselineCandidates, "generated baseline");

        Assert.That(baseline.Baseline.StrictCoverage, Has.Count.EqualTo(1));
        var entry = baseline.Baseline.StrictCoverage[0];
        Assert.That(entry.Id, Is.EqualTo("rule-input-coverage"));
        Assert.That(entry.IgnoredViolations, Has.Some.Matches<ArchitectureBaselineIgnoredViolation>(v =>
            v.SourceType == "video-to-ghost-rule" && v.ForbiddenReference == "ghost"));
        Assert.That(entry.IgnoredViolations, Has.Some.Matches<ArchitectureBaselineIgnoredViolation>(v =>
            v.SourceType == "typo-rule" && v.ForbiddenReference == "does_not_exist_layer"));
    }

    [Test]
    public void Baseline_SuppressesPreviouslyAcceptedUncoveredNamespace_ButFlagsNewOne()
    {
        ArchitectureContractDocument document = CreateNamespaceScopeDocument();
        var context = CreateContext();

        ArchitectureContractRunner generateRunner = new(context, document);
        generateRunner.CheckCoverageContract(document.Contracts.StrictCoverage[0]);

        ArchitectureBaselineDocument baseline = _generator.Generate(
            document, generateRunner.BaselineCandidates, "auto-baseline");

        string baselinePath = Path.Combine(_tempDir, "baseline.yml");
        File.WriteAllText(baselinePath, _generator.Serialize(baseline));

        ArchitectureBaselineDocument loadedBaseline = _loadingService.LoadFromPath(baselinePath);
        ArchitectureBaselineLoadingService.MergeAndValidate(document, loadedBaseline);

        ArchitectureContractRunner gateRunner = new(context, document);
        List<ArchitectureViolation> findings = gateRunner.CheckCoverageContract(document.Contracts.StrictCoverage[0]);

        Assert.That(findings, Is.Empty,
            "All uncovered namespaces from the baseline generation pass should now be suppressed");
    }

    [Test]
    public void Baseline_NewUncoveredNamespaceNotInBaseline_StillFails()
    {
        ArchitectureContractDocument document = CreateNamespaceScopeDocument();
        var context = CreateContext();

        ArchitectureContractRunner generateRunner = new(context, document);
        generateRunner.CheckCoverageContract(document.Contracts.StrictCoverage[0]);

        ArchitectureBaselineDocument baseline = _generator.Generate(
            document, generateRunner.BaselineCandidates, "auto-baseline");

        // Remove one previously-uncovered namespace from the baseline to simulate a namespace
        // that becomes newly uncovered after the baseline was captured.
        baseline.Baseline.StrictCoverage[0].IgnoredViolations.RemoveAll(v =>
            v.SourceType.EndsWith("AlphaGap", StringComparison.Ordinal));

        string baselinePath = Path.Combine(_tempDir, "baseline.yml");
        File.WriteAllText(baselinePath, _generator.Serialize(baseline));

        ArchitectureBaselineDocument loadedBaseline = _loadingService.LoadFromPath(baselinePath);
        ArchitectureBaselineLoadingService.MergeAndValidate(document, loadedBaseline);

        ArchitectureContractRunner gateRunner = new(context, document);
        List<ArchitectureViolation> findings = gateRunner.CheckCoverageContract(document.Contracts.StrictCoverage[0]);

        Assert.That(findings, Has.Some.Matches<ArchitectureViolation>(f =>
            f.SourceType.EndsWith("AlphaGap", StringComparison.Ordinal)),
            "Namespace removed from baseline should still be reported as uncovered");
    }

    [Test]
    public void Baseline_StaleEntry_ResolvedCoverageDebtIsReportedAsUnmatched()
    {
        ArchitectureContractDocument document = new();

        // Scope the contract to a single namespace that starts uncovered (no matching layer),
        // then declare a layer that covers it — simulating the coverage gap having been resolved
        // after the baseline entry below was accepted.
        document.Layers["covered"] = new ArchitectureLayer { Namespace = $"{NamespaceFixtureRoot}.Audio" };

        ArchitectureCoverageContract contract = new()
        {
            Name = "audio-namespace-coverage",
            Id = "audio-namespace-coverage",
            Scope = "namespace",
            Roots = { new ArchitectureCoverageRoot { Namespace = $"{NamespaceFixtureRoot}.Audio" } },
            Reason = "Audio namespace must be covered by a layer.",
            IgnoredViolations =
            {
                new ArchitectureIgnoredViolation
                {
                    SourceType = $"{NamespaceFixtureRoot}.Audio",
                    ForbiddenReference = "uncovered namespace",
                    Reason = "previously accepted"
                }
            }
        };
        document.Contracts.StrictCoverage.Add(contract);

        ArchitectureContractRunner runner = new(CreateContext(), document);
        List<ArchitectureViolation> findings = runner.CheckCoverageContract(contract);

        Assert.That(findings, Is.Empty);
        Assert.That(runner.UnmatchedIgnoredViolations, Has.Some.Matches<ArchitectureUnmatchedIgnoredViolation>(u =>
            u.SourceType == $"{NamespaceFixtureRoot}.Audio" && u.ForbiddenReference == "uncovered namespace"));
    }

    [Test]
    public void Baseline_DoesNotAffectOrdinaryDependencyViolations()
    {
        ArchitectureContractDocument document = CreateRuleInputScopeDocument();

        ArchitectureContractRunner runner = new(CreateContext(), document);

        ArchitectureDependencyContract ordinaryContract = new()
        {
            Id = "video-no-audio",
            Name = "video-no-audio",
            Source = "video",
            Forbidden = { "audio" }
        };

        // Even with coverage baseline candidates tracked on the same runner, ordinary dependency
        // violations for an unrelated contract must still be reported in full.
        runner.CheckCoverageContract(document.Contracts.StrictCoverage[0]);
        List<ArchitectureViolation> ordinaryViolations = runner.CheckContract(ordinaryContract);

        Assert.That(ordinaryViolations, Is.Not.Empty);
    }

    [Test]
    public void AuditCoverageBaseline_NonBlockingFindingsAreStillBaselineable()
    {
        ArchitectureContractDocument document = CreateNamespaceScopeDocument();
        ArchitectureCoverageContract strictContract = document.Contracts.StrictCoverage[0];
        document.Contracts.StrictCoverage.Clear();

        ArchitectureCoverageContract auditContract = new()
        {
            Name = strictContract.Name,
            Id = strictContract.Id,
            Scope = strictContract.Scope,
            Roots = strictContract.Roots,
            Reason = strictContract.Reason
        };
        document.Contracts.AuditCoverage.Add(auditContract);

        ArchitectureContractRunner runner = new(CreateContext(), document);
        runner.CheckCoverageContract(auditContract);

        ArchitectureBaselineDocument baseline = _generator.Generate(
            document, runner.BaselineCandidates, "generated baseline");

        Assert.That(baseline.Baseline.AuditCoverage, Has.Count.EqualTo(1));
        Assert.That(baseline.Baseline.StrictCoverage, Is.Empty);
    }

    [Test]
    public void Loader_LegacyBaselineFileWithoutCoverageKeys_LoadsAndValidatesUnchanged()
    {
        const string LegacyYaml = "version: 1\nbaseline:\n  strict:\n    - id: \"legacy-rule\"\n      ignored_violations:\n        - source_type: \"A.B\"\n          forbidden_reference: \"C.D\"\n          reason: \"legacy\"\n";

        string baselinePath = Path.Combine(_tempDir, "legacy-baseline.yml");
        File.WriteAllText(baselinePath, LegacyYaml);

        ArchitectureBaselineDocument loaded = _loadingService.LoadFromPath(baselinePath);

        Assert.That(loaded.Baseline.Strict, Has.Count.EqualTo(1));
        Assert.That(loaded.Baseline.StrictCoverage, Is.Empty);
        Assert.That(loaded.Baseline.AuditCoverage, Is.Empty);
    }
}
