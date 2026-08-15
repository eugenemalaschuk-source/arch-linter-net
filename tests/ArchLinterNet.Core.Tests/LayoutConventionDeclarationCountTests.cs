using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayoutConventionDeclarationCountTests
{
    private static readonly string[] _partialOffenderDeclarationPaths =
    [
        "Elsewhere/PartialOffender.Part2.cs",
        "Services/PartialOffender.Part1.cs",
    ];

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-layout-declaration-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        WriteFixtureFile("Services/PartialOffender.Part1.cs",
            "namespace LayoutConventionContractTestFixtures.AmbiguousFolder { public sealed class PartialOffender { } }");
        WriteFixtureFile("Elsewhere/PartialOffender.Part2.cs",
            "namespace LayoutConventionContractTestFixtures.AmbiguousFolder { public sealed class PartialOffender { } }");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void CheckLayoutConventionsContract_MaxDeclarationsPerType_ReportsMeasuredPartialDeclarationCount()
    {
        string assemblyName = typeof(LayoutConventionContractTests).Assembly.GetName().Name!;
        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, $"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{assemblyName}]
              source_roots: ["."]
            contracts:
              strict_layout_conventions:
                - name: services-single-declaration
                  files_matching:
                    folder_segment: Services
                  max_declarations_per_type: 1
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        ArchitectureLayoutConventionContract contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        IReadOnlyList<ArchitectureViolation> violations = runner.Session.CheckLayoutConventionsContract(contract);
        ArchitectureViolation violation = violations.Single(v =>
            v.SourceType.Contains("PartialOffender", StringComparison.Ordinal));
        var payload = (LayoutConventionPayload)violation.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(payload.DataUnavailable, Is.False);
            Assert.That(payload.ExpectedDeclarationCount, Is.EqualTo(1));
            Assert.That(payload.ActualDeclarationCount, Is.EqualTo(2));
            Assert.That(payload.DeclarationPaths, Is.EqualTo(_partialOffenderDeclarationPaths));
            Assert.That(violation.ForbiddenNamespace, Does.Contain("expected at most 1 source declaration"));
        });
    }

    [Test]
    public void CheckLayoutConventionsContract_MaxDeclarationsPerType_AuditRuleUsesSameEvaluation()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "audit-services-single-declaration",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            MaxDeclarationsPerType = 1,
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, audit: true));

        IReadOnlyList<ArchitectureViolation> violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Single(violation =>
            violation.SourceType.Contains("PartialOffender", StringComparison.Ordinal)).Payload,
            Is.TypeOf<LayoutConventionPayload>());
    }

    [Test]
    public void CheckLayoutConventionsContract_MaxDeclarationsPerType_WithoutSourceInventoryReportsUnavailable()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-single-declaration",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            MaxDeclarationsPerType = 1,
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, withSourceRoots: false));

        IReadOnlyList<ArchitectureViolation> violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations.Single().Payload, Is.TypeOf<LayoutConventionPayload>()
            .And.Property(nameof(LayoutConventionPayload.DataUnavailable)).True);
    }

    private void WriteFixtureFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(_tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private ArchitectureAnalysisContext CreateContext() => new(
        _tempDir,
        new[] { typeof(LayoutConventionContractTests).Assembly },
        Array.Empty<string>(),
        Array.Empty<string>(),
        null,
        projectDiscovery: null);

    private static ArchitectureContractDocument CreateDocument(
        ArchitectureLayoutConventionContract contract,
        bool audit = false,
        bool withSourceRoots = true)
    {
        var groups = new ArchitectureContractGroups();
        if (audit)
        {
            groups.AuditLayoutConventions = new List<ArchitectureLayoutConventionContract> { contract };
        }
        else
        {
            groups.StrictLayoutConventions = new List<ArchitectureLayoutConventionContract> { contract };
        }

        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(LayoutConventionContractTests).Assembly.GetName().Name! },
                SourceRoots = withSourceRoots ? new List<string> { "." } : new List<string>()
            },
            Contracts = groups
        };
    }
}
