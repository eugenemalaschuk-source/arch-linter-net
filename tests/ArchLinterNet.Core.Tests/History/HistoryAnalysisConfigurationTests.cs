using System.Text;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Tasks;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryAnalysisConfigurationTests
{
    private static readonly string[] _extractedTaskKeys = { "issue#2", "jira#1" };
    private static readonly string[] _extractedMatchedTexts = { "JIRA-001", "#2" };
    private static readonly string[] _importedExtractorIds = { "jira" };
    private static readonly string[] _importedProductionPaths = { "src/**" };
    private static readonly string[] _importedDocsPaths = { "docs/**" };
    private static readonly string[] _importedIgnorePaths = { "src/generated/**" };

    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "arch-linter-history-configuration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void AbsentHistoryAnalysisUsesTheReviewedDefaultProfiles()
    {
        ArchitectureContractDocument document = Load(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(document.HistoryAnalysis.Extractors, Is.Empty);
            Assert.That(document.HistoryAnalysis.Ignore, Is.Empty);
            Assert.That(document.HistoryAnalysis.Thresholds.CoChangeSignificance, Is.Null);
            Assert.That(document.HistoryAnalysis.Weights.Hotspot.Commit, Is.EqualTo(0.30m));
            Assert.That(document.HistoryAnalysis.Weights.CoChange.Task, Is.EqualTo(0.25m));
            Assert.That(document.HistoryAnalysis.Weights.Bottleneck.IndependentTask, Is.EqualTo(0.35m));
            Assert.That(document.HistoryAnalysis.Weights.Ocp.RoleHint, Is.EqualTo(0.10m));
        });
    }

    [Test]
    public void ConfiguredLiteralExtractorFeedsCanonicalTaskKeyExtraction()
    {
        ArchitectureContractDocument document = Load(
            """
            history_analysis:
              extractors:
                - id: jira
                  namespace: jira
                  pattern:
                    prefix: JIRA-
            """);

        (IReadOnlyList<TaskKeyMatch> matches, IReadOnlyList<TaskKey> keys) = TaskKeyExtraction
            .FromConfiguration(document.HistoryAnalysis)
            .Extract(Encoding.UTF8.GetBytes("fix JIRA-001 and #2"), "commit");

        Assert.Multiple(() =>
        {
            Assert.That(keys.Select(static key => key.ToString()), Is.EqualTo(_extractedTaskKeys));
            Assert.That(matches.Select(static match => match.MatchedText), Is.EqualTo(_extractedMatchedTexts));
            Assert.That(matches[0].SpanStart, Is.EqualTo(4));
            Assert.That(matches[0].SpanEnd, Is.EqualTo(12));
        });
    }

    [Test]
    public void PolicyCheckSchemaAcceptsTheBoundedHistoryAnalysisSection()
    {
        string path = WritePolicy(
            """
            history_analysis:
              paths:
                production: [src/**]
              ignore: [src/generated/**]
              thresholds:
                co_change_significance: 0.750000000
            """);

        ArchitectureContractDocument document = ((IArchitecturePolicyCheckDocumentLoader)new ArchitecturePolicyDocumentLoader())
            .LoadForPolicyCheck(path);

        Assert.That(document.HistoryAnalysis.Thresholds.CoChangeSignificance, Is.EqualTo(0.750000000m));
    }

    [Test]
    public void ImportedHistoryAnalysisIsComposedIntoTheEffectivePolicy()
    {
        string root = WriteFile(
            "architecture/root.yml",
            """
            version: 1
            name: Imported history configuration
            imports: [history.yml]
            layers: {}
            analysis:
              target_assemblies: [App]
            contracts:
              strict: []
            history_analysis:
              paths:
                docs: [docs/**]
            """);
        WriteFile(
            "architecture/history.yml",
            """
            history_analysis:
              extractors:
                - id: jira
                  namespace: jira
                  pattern:
                    prefix: JIRA-
              paths:
                production: [src/**]
              ignore: [src/generated/**]
              weights:
                co_change:
                  commit: 0.75
                  task: 0.25
              thresholds:
                co_change_significance: 0.750000000
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);
        (IReadOnlyList<TaskKeyMatch> matches, IReadOnlyList<TaskKey> keys) = TaskKeyExtraction
            .FromConfiguration(document.HistoryAnalysis)
            .Extract(Encoding.UTF8.GetBytes("JIRA-001 #2"), "commit");
        var classifier = new HistoryPathClassifier(document.HistoryAnalysis);

        Assert.Multiple(() =>
        {
            Assert.That(document.HistoryAnalysis.Extractors.Select(static extractor => extractor.Id), Is.EqualTo(_importedExtractorIds));
            Assert.That(document.HistoryAnalysis.Paths.Production, Is.EqualTo(_importedProductionPaths));
            Assert.That(document.HistoryAnalysis.Paths.Docs, Is.EqualTo(_importedDocsPaths));
            Assert.That(document.HistoryAnalysis.Ignore, Is.EqualTo(_importedIgnorePaths));
            Assert.That(document.HistoryAnalysis.Weights.CoChange.Commit, Is.EqualTo(0.75m));
            Assert.That(document.HistoryAnalysis.Thresholds.CoChangeSignificance, Is.EqualTo(0.750000000m));
            Assert.That(matches.Select(static match => match.MatchedText), Is.EqualTo(_extractedMatchedTexts));
            Assert.That(keys.Select(static key => key.ToString()), Is.EqualTo(_extractedTaskKeys));
            Assert.That(classifier.Classify("src/service.cs").Category, Is.EqualTo(HistoryPathCategory.Production));
            Assert.That(classifier.Classify("docs/guide.md").Category, Is.EqualTo(HistoryPathCategory.Docs));
            Assert.That(classifier.Classify("src/generated/code.cs").IsIgnored, Is.True);
        });
    }

    [Test]
    public void InvalidImportedHistoryPathReportsItsFragmentProvenance()
    {
        string root = WriteFile(
            "architecture/root.yml",
            """
            version: 1
            name: Imported history provenance
            imports: [history.yml]
            layers: {}
            analysis:
              target_assemblies: [App]
            contracts:
              strict: []
            """);
        WriteFile(
            "architecture/history.yml",
            """
            history_analysis:
              paths:
                production: [src/partial*.cs]
            """);

        ArchitecturePolicyValidationException exception = Assert.Throws<ArchitecturePolicyValidationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;
        ArchitecturePolicyDiagnostic locationDiagnostic = exception.Diagnostic;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("supports only whole-segment"));
            Assert.That(locationDiagnostic.Location, Is.Not.Null);
            Assert.That(locationDiagnostic.Location!.SourcePath, Is.EqualTo("architecture/history.yml"));
            Assert.That(locationDiagnostic.Location.YamlPath, Is.EqualTo("history_analysis.paths.production[0]"));
        });
    }

    [TestCase("history_analysis:\n  unknwon: true\n", "unknown property 'unknwon'")]
    [TestCase("history_analysis:\n  extractors:\n    - id: issue\n      namespace: issue\n      pattern:\n        prefix: ISSUE-\n", "reserved")]
    [TestCase("history_analysis:\n  weights:\n    hotspot:\n      commit: 0.30\n      churn: 0.25\n      task: 0.25\n      author: 0.10\n      temporal: 0.09\n", "sum exactly")]
    [TestCase("history_analysis:\n  weights:\n    co_change:\n      commit: \"0.75\"\n      task: 0.25\n", "nonnegative base-10 decimal")]
    [TestCase("history_analysis:\n  thresholds:\n    co_change_significance: 1.000000001\n", "must be in [0,1]")]
    public void InvalidConfigurationFailsPolicyLoadingBeforeAnalysis(string historyAnalysis, string expectedMessage)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load(historyAnalysis))!;

        Assert.That(exception.Message, Does.Contain(expectedMessage));
    }

    [Test]
    public void PathClassificationUsesExactScalarsAndIgnorePrecedesTheFixedCategoryOrder()
    {
        var configuration = new HistoryAnalysisConfiguration
        {
            Ignore = ["src/generated/**"],
            Paths = new HistoryPathConfiguration
            {
                Production = ["src/**", "src/caf\u00E9.cs"],
                Docs = ["docs/**"],
            },
        };
        var classifier = new HistoryPathClassifier(configuration);

        HistoryPathClassification production = classifier.Classify("src/service.cs");
        HistoryPathClassification ignored = classifier.Classify("src/generated/service.cs");
        HistoryPathClassification distinctUnicode = classifier.Classify("src/cafe\u0301.cs");

        Assert.Multiple(() =>
        {
            Assert.That(production, Is.EqualTo(new HistoryPathClassification(false, HistoryPathCategory.Production)));
            Assert.That(ignored.IsIgnored, Is.True);
            Assert.That(distinctUnicode.Category, Is.EqualTo(HistoryPathCategory.Production),
                "The general src/** configuration is an intentional broader match.");
        });
    }

    [Test]
    public void PathClassificationDoesNotNormalizeDistinctUnicodeSpellings()
    {
        var classifier = new HistoryPathClassifier(new HistoryAnalysisConfiguration
        {
            Paths = new HistoryPathConfiguration { Production = ["src/caf\u00E9.cs"] },
        });

        Assert.Multiple(() =>
        {
            Assert.That(classifier.Classify("src/caf\u00E9.cs").Category, Is.EqualTo(HistoryPathCategory.Production));
            Assert.That(classifier.Classify("src/cafe\u0301.cs").Category, Is.EqualTo(HistoryPathCategory.Unknown));
        });
    }

    private ArchitectureContractDocument Load(string historyAnalysis)
    {
        string path = WritePolicy(historyAnalysis);
        return new ArchitecturePolicyDocumentLoader().Load(path);
    }

    private string WritePolicy(string historyAnalysis)
    {
        string path = Path.Combine(_temporaryDirectory, "architecture.arch.yml");
        File.WriteAllText(path, $$"""
            version: 1
            name: History configuration tests
            layers: {}
            analysis:
              target_assemblies: [App]
            contracts:
              strict: []
            {{historyAnalysis}}
            """);
        return path;
    }

    private string WriteFile(string relativePath, string yaml)
    {
        string path = Path.Combine(_temporaryDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, yaml);
        return path;
    }
}
