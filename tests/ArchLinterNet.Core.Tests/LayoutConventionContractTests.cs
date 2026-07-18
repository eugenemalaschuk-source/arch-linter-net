using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayoutConventionContractTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-layout-convention-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        WriteFixtureFile("Services/OrderService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public sealed class OrderService { } }");
        WriteFixtureFile("Services/PaymentService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public sealed class PaymentService { } }");
        WriteFixtureFile("Services/IWronglyPlacedService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public interface IWronglyPlacedService { } }");
        WriteFixtureFile("Interfaces/IOrderService.cs",
            "namespace LayoutConventionContractTestFixtures.Interfaces { public interface IOrderService { } }");
        WriteFixtureFile("Interfaces/WronglyPlacedClass.cs",
            "namespace LayoutConventionContractTestFixtures.Interfaces { public sealed class WronglyPlacedClass { } }");
        WriteFixtureFile("MismatchedFileName/DifferentFileName.cs",
            "namespace LayoutConventionContractTestFixtures.MismatchedFileName { public sealed class ActualTypeName { } }");
        WriteFixtureFile("WhenRefinement/IncludedByWhen.cs",
            "namespace LayoutConventionContractTestFixtures.WhenRefinement { public sealed class IncludedByWhen { } }");
        WriteFixtureFile("WhenRefinement/ExcludedByWhen.cs",
            "namespace LayoutConventionContractTestFixtures.WhenRefinement { public sealed class ExcludedByWhen { } }");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private void WriteFixtureFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(_tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(LayoutConventionContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            projectDiscovery: null);
    }

    private ArchitectureContractDocument CreateDocument(
        ArchitectureLayoutConventionContract contract,
        bool audit = false,
        bool withSourceRoots = true,
        List<ArchitectureIgnoredViolation>? ignoredViolations = null)
    {
        if (ignoredViolations != null)
        {
            contract.IgnoredViolations = ignoredViolations;
        }

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

    [Test]
    public void CheckLayoutConventionsContract_RequireTypeKind_ServicesFolderMustContainClass_Passes()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-folder-must-contain-classes",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            RequireTypeKind = "class"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        // OrderService.cs and PaymentService.cs both declare a class; each file individually
        // satisfies require_type_kind, so there is no violation for those files.
        Assert.That(violations.Any(v => v.SourceType.Contains("Services/OrderService.cs", StringComparison.Ordinal)), Is.False);
        Assert.That(violations.Any(v => v.SourceType.Contains("Services/PaymentService.cs", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckLayoutConventionsContract_ForbidTypeKind_InterfaceInServicesFolder_IsViolation()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-folder-must-not-contain-interfaces",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            ForbidTypeKind = "interface"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("IWronglyPlacedService", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void CheckLayoutConventionsContract_RequireTypeKind_InterfacesFolderMustContainInterface_ConcreteClassIsViolation()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "interfaces-folder-must-contain-interfaces",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Interfaces" },
            ForbidTypeKind = "class"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("WronglyPlacedClass", StringComparison.Ordinal)), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType.Contains("IOrderService", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckLayoutConventionsContract_RequireMatchingInterface_MissingCounterpart_IsViolation()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-require-matching-interface",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            RequireMatchingInterface = new ArchitectureRequireMatchingInterface { NamePrefix = "I" }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("PaymentService", StringComparison.Ordinal)
            && v.ForbiddenNamespace.Contains("IPaymentService", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void CheckLayoutConventionsContract_RequireMatchingInterface_PresentCounterpart_Passes()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-require-matching-interface",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            RequireMatchingInterface = new ArchitectureRequireMatchingInterface { NamePrefix = "I" }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("OrderService", StringComparison.Ordinal)
            && !v.SourceType.Contains("IOrderService", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckLayoutConventionsContract_RequireTypeNameMatchesFileName_Mismatch_IsViolation()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "type-name-must-match-file-name",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "MismatchedFileName" },
            RequireTypeNameMatchesFileName = true
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("DifferentFileName.cs", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void CheckLayoutConventionsContract_WhenRefinement_NarrowsMatchedDeclaredTypes()
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
                - name: when-refined-naming
                  files_matching:
                    folder_segment: WhenRefinement
                    when: subject.simpleName == "IncludedByWhen"
                  required_name_suffix: DoesNotMatchAnything
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("IncludedByWhen", StringComparison.Ordinal)), Is.True);
        Assert.That(violations.Any(v => v.SourceType.Contains("ExcludedByWhen", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckLayoutConventionsContract_NoSourceEnrichedFacts_EmitsUnavailableDiagnostic()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-folder-must-contain-classes",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            RequireTypeKind = "class"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, withSourceRoots: false));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].Payload, Is.InstanceOf<LayoutConventionPayload>());
        Assert.That(((LayoutConventionPayload)violations[0].Payload!).DataUnavailable, Is.True);
    }

    [Test]
    public void CheckLayoutConventionsContract_IgnoredViolation_SuppressesMatch()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-folder-must-not-contain-interfaces",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            ForbidTypeKind = "interface"
        };
        List<ArchitectureIgnoredViolation> ignores = new()
        {
            new ArchitectureIgnoredViolation
            {
                SourceType = "LayoutConventionContractTestFixtures.Services.IWronglyPlacedService",
                ForbiddenReference = "forbidden type kind 'interface'",
                Reason = "legacy"
            }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, ignoredViolations: ignores));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("IWronglyPlacedService", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckLayoutConventionsContract_AuditMode_ReturnsViolationsSameAsStrict()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-folder-must-not-contain-interfaces",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            ForbidTypeKind = "interface"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, audit: true));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("IWronglyPlacedService", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void CheckLayoutConventionsContract_DeterministicOrdering_AcrossRepeatedRuns()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-folder-must-not-contain-interfaces",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            ForbidTypeKind = "interface"
        };
        var document = CreateDocument(contract);
        var firstRun = new ArchitectureContractRunner(CreateContext(), document).Session.CheckLayoutConventionsContract(contract);
        var secondRun = new ArchitectureContractRunner(CreateContext(), document).Session.CheckLayoutConventionsContract(contract);

        Assert.That(firstRun.Select(v => v.SourceType), Is.EqualTo(secondRun.Select(v => v.SourceType)));
    }
}
