using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using static ArchLinterNet.Core.Tests.LayoutConventionContractTests;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayoutConventionExclusionTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-layout-convention-exclusion-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        WriteFixtureFile("Services/OrderService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public sealed class OrderService { } }");
        WriteFixtureFile("Services/PaymentService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public sealed class PaymentService { } }");
        WriteFixtureFile("Services/PartialOffender.Part1.cs",
            "namespace LayoutConventionContractTestFixtures.AmbiguousFolder { public sealed class PartialOffender { } }");
        WriteFixtureFile("Elsewhere/PartialOffender.Part2.cs",
            "namespace LayoutConventionContractTestFixtures.AmbiguousFolder { public sealed class PartialOffender { } }");
        WriteFixtureFile("Services/IWronglyPlacedService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public interface IWronglyPlacedService { } }");
        WriteFixtureFile("Interfaces/IOrderService.cs",
            "namespace LayoutConventionContractTestFixtures.Interfaces { public interface IOrderService { } }");
        WriteFixtureFile("Interfaces/WronglyPlacedClass.cs",
            "namespace LayoutConventionContractTestFixtures.Interfaces { public sealed class WronglyPlacedClass { } }");
        WriteFixtureFile("WhenRefinement/IncludedByWhen.cs",
            "namespace LayoutConventionContractTestFixtures.WhenRefinement { public sealed class IncludedByWhen { } }");
        WriteFixtureFile("WhenRefinement/ExcludedByWhen.cs",
            "namespace LayoutConventionContractTestFixtures.WhenRefinement { public sealed class ExcludedByWhen { } }");
        WriteFixtureFile("MixedNamespaceFile/Mixed.cs", "namespace LayoutConventionContractTestFixtures.MixedNamespaceFile { public sealed class ServiceInMatchingNamespace { } }\nnamespace LayoutConventionContractTestFixtures.MixedNamespaceFileOther { public interface IEscapingInterface { } }");
        WriteFixtureFile("AbstractServices/AbstractBaseService.cs",
            "namespace LayoutConventionContractTestFixtures.AbstractServices { public abstract class AbstractBaseService { } }");
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

    [Test]
    public void CheckLayoutConventionsContract_ExcludeWhen_SubtractsMatchedDeclaredTypes()
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
                - name: exclusion-when
                  files_matching:
                    folder_segment: WhenRefinement
                  exclude_files_matching:
                    - folder_segment: WhenRefinement
                      when: subject.simpleName == "ExcludedByWhen"
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
    public void CheckLayoutConventionsContract_MaxDeclarationsPerType_HonorsWhenAndExclusion()
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
                - name: partial-declaration-budget
                  files_matching:
                    folder_segment: Services
                    when: subject.simpleName == "PartialOffender"
                  exclude_files_matching:
                    - folder_segment: Elsewhere
                  max_declarations_per_type: 1
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        IReadOnlyList<ArchitectureViolation> violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.Multiple(() =>
        {
            Assert.That(violations.Any(violation =>
                violation.SourceType.Contains("PartialOffender", StringComparison.Ordinal)), Is.False);
            Assert.That(
                runner.SubtractiveMatcherParticipation.Select(participation =>
                    (participation.Kind, participation.Field, participation.Index, participation.Matched)),
                Is.EqualTo(new[]
                {
                    (ArchitectureSelectorParticipationKind.Inclusion, "files_matching", (int?)null, true),
                    (ArchitectureSelectorParticipationKind.Exclusion, "exclude_files_matching", 0, true),
                }));
        });
    }

    [Test]
    public void CheckLayoutConventionsContract_ExcludeFilesMatching_RecordsMatchedAndStaleParticipation()
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
                - name: exclusion-when
                  files_matching:
                    folder_segment: WhenRefinement
                  exclude_files_matching:
                    - folder_segment: WhenRefinement
                      when: subject.simpleName == "ExcludedByWhen"
                    - folder_segment: NoSuchFolderAnywhere
                  required_name_suffix: DoesNotMatchAnything
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(
            runner.SubtractiveMatcherParticipation.Select(p => (p.Kind, p.Field, p.Index, p.Matched)),
            Is.EqualTo(new[]
            {
                (ArchitectureSelectorParticipationKind.Inclusion, "files_matching", (int?)null, true),
                (ArchitectureSelectorParticipationKind.Exclusion, "exclude_files_matching", 0, true),
                (ArchitectureSelectorParticipationKind.Exclusion, "exclude_files_matching", 1, false)
            }),
            "The first exclusion actually subtracted a candidate and must report matched; the " +
            "second targets a folder that doesn't exist and must report stale.");
    }

    [Test]
    public void CheckLayoutConventionsContract_ExclusionMatchesOnlyAlreadyExcludedType_ReportsStaleNotMatched()
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
                - name: exclusion-against-eligible-set
                  files_matching:
                    folder_segment: MixedNamespaceFile
                    when: subject.simpleName == "ServiceInMatchingNamespace"
                  exclude_files_matching:
                    - folder_segment: MixedNamespaceFile
                      when: subject.simpleName == "IEscapingInterface"
                  required_name_suffix: DoesNotMatchAnything
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        // IEscapingInterface was never part of the eligible set (files_matching's own `when` never
        // included it), so the exclusion - which only ever matches IEscapingInterface - subtracted
        // nothing and must report stale, not matched. ServiceInMatchingNamespace must still be
        // flagged, proving the eligible set itself is untouched.
        Assert.Multiple(() =>
        {
            Assert.That(violations.Any(v => v.SourceType.Contains("ServiceInMatchingNamespace", StringComparison.Ordinal)), Is.True);
            Assert.That(
                runner.SubtractiveMatcherParticipation.Select(p => (p.Kind, p.Field, p.Index, p.Matched)),
                Is.EqualTo(new[]
                {
                    (ArchitectureSelectorParticipationKind.Inclusion, "files_matching", (int?)null, true),
                    (ArchitectureSelectorParticipationKind.Exclusion, "exclude_files_matching", 0, false)
                }));
        });
    }

    [Test]
    public void CheckLayoutConventionsContract_NoSourceEnrichedFacts_RecordsExclusionEvaluationFailed()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-folder-must-contain-classes",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            ExcludeFilesMatching = { new ArchitectureLayoutFileMatcher { FolderSegment = "Generated" } },
            RequireTypeKind = "class"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, withSourceRoots: false));

        runner.Session.CheckLayoutConventionsContract(contract);

        // The whole run aborted before any exclusion got evaluated - the exclusion needs
        // source-path facts just like the include selector, so it must still surface as
        // evaluation-failed rather than silently vanish from the participation result.
        ArchitectureSubtractiveMatcherParticipation participation = runner.SubtractiveMatcherParticipation
            .Single(item => item.Kind == ArchitectureSelectorParticipationKind.Exclusion);
        Assert.Multiple(() =>
        {
            Assert.That(participation.EvaluationFailed, Is.True);
            Assert.That(participation.Matched, Is.False);
        });
    }

    [Test]
    public void CheckLayoutConventionsContract_AmbiguousPartialType_ExcludeMatcherSuppressesViolation()
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
                - name: services-must-not-contain-offenders
                  files_matching:
                    folder_segment: Services
                    when: subject.simpleName == "PartialOffender"
                  exclude_files_matching:
                    - folder_segment: Services
                      when: subject.simpleName == "PartialOffender"
                  forbid_type_kind: class
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("PartialOffender", StringComparison.Ordinal)), Is.False);
        Assert.That(runner.SubtractiveMatcherParticipation.Single(item =>
            item.Kind == ArchitectureSelectorParticipationKind.Inclusion).Matched, Is.True,
            "The ambiguous declaration entered files_matching before the exclusion suppressed it.");
    }

    [Test]
    public void CheckLayoutConventionsContract_ExcludeWhenReferencesSourcePaths_PartialEnrichment_UnfiledCandidateIsViolation()
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
                - name: path-based-exclusion
                  files_matching:
                    namespace_segment: UnfiledNamespace
                  exclude_files_matching:
                    - namespace_segment: UnfiledNamespace
                      when: subject.sourcePaths.size() > 0
                  forbid_type_kind: interface
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("NoSourceFileType", StringComparison.Ordinal)
            && v.Payload is LayoutConventionPayload { DataUnavailable: true }), Is.True);

        // The exclusion's `when` couldn't be evaluated for this candidate (no resolved source
        // file) - it must report neither Matched nor stale, since whether it would have excluded
        // the candidate is genuinely unknown, not "no".
        ArchitectureSubtractiveMatcherParticipation participation = runner.SubtractiveMatcherParticipation
            .Single(item => item.Kind == ArchitectureSelectorParticipationKind.Exclusion);
        Assert.Multiple(() =>
        {
            Assert.That(participation.EvaluationFailed, Is.True);
            Assert.That(participation.Matched, Is.False);
        });
    }
}
