using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
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
        // Both types below share ONE physical file but declare different namespaces - regression
        // fixture for file-level (not fact-level) selector matching: once the file matches via
        // ServiceInMatchingNamespace's namespace, IEscapingInterface must not be able to dodge
        // expectations just because its own namespace segment differs.
        WriteFixtureFile("MixedNamespaceFile/Mixed.cs", """
            namespace LayoutConventionContractTestFixtures.MixedNamespaceFile { public sealed class ServiceInMatchingNamespace { } }
            namespace LayoutConventionContractTestFixtures.MixedNamespaceFileOther { public interface IEscapingInterface { } }
            """);
        // Same namespace+type name declared in two distinct files - a partial-class ambiguity per
        // ArchitectureSourceFileFactIndex, giving PartialOffender a null SourceFilePath even though
        // one of its two candidate declaration paths sits under "Services".
        WriteFixtureFile("Services/PartialOffender.Part1.cs",
            "namespace LayoutConventionContractTestFixtures.AmbiguousFolder { public sealed class PartialOffender { } }");
        WriteFixtureFile("Elsewhere/PartialOffender.Part2.cs",
            "namespace LayoutConventionContractTestFixtures.AmbiguousFolder { public sealed class PartialOffender { } }");
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

    private static ArchitectureContractDocument CreateDocument(
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

    // Regression: file selection must be file-granular, not fact-granular. Both types below share one
    // physical file but declare different namespaces; the file matches via ServiceInMatchingNamespace's
    // namespace segment, so IEscapingInterface (declared under a different namespace in that same file)
    // must still be caught by forbid_type_kind instead of escaping because its own namespace differs.
    [Test]
    public void CheckLayoutConventionsContract_NamespaceSegmentMatch_AppliesToWholeFileNotJustMatchingFact()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "matching-namespace-folder-must-not-contain-interfaces",
            FilesMatching = new ArchitectureLayoutFileMatcher { NamespaceSegment = "MixedNamespaceFile" },
            ForbidTypeKind = "interface"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("IEscapingInterface", StringComparison.Ordinal)), Is.True,
            "A type in the same matched file must not escape expectations by declaring a different namespace.");
    }

    // Regression: only folder_segment/file_name_* selector fields require source-enriched facts.
    // A namespace_segment-only contract must keep working from reflection-derived namespace facts
    // even when no source_roots is configured, instead of being unconditionally disabled.
    [Test]
    public void CheckLayoutConventionsContract_NamespaceSegmentOnly_WorksWithoutSourceRoots()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-namespace-must-not-contain-interfaces",
            FilesMatching = new ArchitectureLayoutFileMatcher { NamespaceSegment = "Services" },
            ForbidTypeKind = "interface"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, withSourceRoots: false));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.Payload is LayoutConventionPayload { DataUnavailable: true }), Is.False,
            "A namespace_segment-only contract must not report path-based-checks-unavailable.");
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

    // Routes through ArchitectureContractFamilyRegistry's "layout_conventions" Checker delegate and
    // ArchitectureContractHandlerRegistry, not just the direct session call every other test in this
    // file uses - exercises the family's actual dispatch wiring end-to-end, matching how the CLI runs it.
    [Test]
    public void Executor_RoutesLayoutConventionsThroughRegistry_MatchesDirectSessionCall()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-folder-must-not-contain-interfaces",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            ForbidTypeKind = "interface"
        };
        var document = CreateDocument(contract);

        var directRunner = new ArchitectureContractRunner(CreateContext(), document);
        List<ArchitectureViolation> expected = directRunner.Session.CheckLayoutConventionsContract(contract);

        var executorRunner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContractExecutionResult result = new ArchitectureContractExecutor()
            .Execute(executorRunner.Session, "strict", new ArchitectureContractHandlerRegistry());

        Assert.That(result.Violations.Select(v => v.SourceType), Is.EquivalentTo(expected.Select(v => v.SourceType)));
        Assert.That(result.Violations, Has.Count.GreaterThan(0));
    }

    // Regression: require_type_name_matches_file_name inherently needs a resolved file name.
    // A namespace_segment-only contract combined with it, run with no source_roots configured at
    // all, must not silently report zero violations forever - the run-level guard now treats
    // require_type_name_matches_file_name as needing source-path data too.
    [Test]
    public void CheckLayoutConventionsContract_RequireTypeNameMatchesFileName_NoSourceRootsAtAll_EmitsUnavailableDiagnostic()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "namespace-only-file-name-match",
            FilesMatching = new ArchitectureLayoutFileMatcher { NamespaceSegment = "Services" },
            RequireTypeNameMatchesFileName = true
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, withSourceRoots: false));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(((LayoutConventionPayload)violations[0].Payload!).DataUnavailable, Is.True);
    }

    // Regression: even when SOME facts in the run are source-enriched (so the run-level guard does
    // not fire), a namespace_segment match can still land on a type with no resolvable source file.
    // require_type_name_matches_file_name must report that as a violation, not silently skip it.
    [Test]
    public void CheckLayoutConventionsContract_RequireTypeNameMatchesFileName_UnfiledMatch_IsViolationNotSilentPass()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "namespace-only-file-name-match",
            FilesMatching = new ArchitectureLayoutFileMatcher { NamespaceSegment = "UnfiledNamespace" },
            RequireTypeNameMatchesFileName = true
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("NoSourceFileType", StringComparison.Ordinal)
            && v.Payload is LayoutConventionPayload { DataUnavailable: true }), Is.True);
    }

    // Regression: a partial-class declaration spread across two files gets a null SourceFilePath
    // (ambiguous), which previously made it silently invisible to folder_segment-based rules even
    // though one of its candidate declaration paths sits under the flagged folder.
    [Test]
    public void CheckLayoutConventionsContract_AmbiguousPartialType_CandidatePathMatchesFolder_IsViolation()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "services-must-not-contain-offenders",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            ForbidTypeKind = "class"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("PartialOffender", StringComparison.Ordinal)
            && v.Payload is LayoutConventionPayload { DataUnavailable: true }), Is.True);
    }

    // Regression: an ambiguity whose candidate paths do NOT satisfy the folder/file-name selector
    // must not be reported - only ambiguities that could plausibly match are flagged as unresolvable.
    [Test]
    public void CheckLayoutConventionsContract_AmbiguousPartialType_NoCandidatePathMatches_NoViolation()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "interfaces-must-not-contain-offenders",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Interfaces" },
            ForbidTypeKind = "class"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("PartialOffender", StringComparison.Ordinal)), Is.False);
    }

    // Regression: reflection alone classifies records as Class/Struct (see source-file-fact-index) -
    // a record type with no resolvable source file must not silently pass forbid_type_kind: record.
    [Test]
    public void CheckLayoutConventionsContract_ForbidRecordKind_UnresolvedSourceFile_IsViolationNotSilentPass()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "records-forbidden-in-record-kind-namespace",
            FilesMatching = new ArchitectureLayoutFileMatcher { NamespaceSegment = "RecordKind" },
            ForbidTypeKind = "record"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("UnresolvedRecord", StringComparison.Ordinal)
            && v.Payload is LayoutConventionPayload { DataUnavailable: true }), Is.True);
    }

    // Regression: the run-level guard must also treat a record type-kind expectation as needing
    // source-path data, so a namespace_segment-only contract with zero source enrichment gets the
    // single clean "unavailable" diagnostic instead of silently reporting zero violations.
    [Test]
    public void CheckLayoutConventionsContract_ForbidRecordKind_NoSourceRootsAtAll_EmitsUnavailableDiagnostic()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "records-forbidden",
            FilesMatching = new ArchitectureLayoutFileMatcher { NamespaceSegment = "Services" },
            ForbidTypeKind = "record"
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract, withSourceRoots: false));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(((LayoutConventionPayload)violations[0].Payload!).DataUnavailable, Is.True);
    }

    // Regression: subject.sourcePaths/sourceDirectoryPrefixes are empty lists (not an evaluation
    // error) for a candidate with no resolved source file, so a `when` referencing them would
    // otherwise silently exclude every candidate on a fully-unenriched run and look like a clean
    // pass instead of the "unavailable" diagnostic every other path-dependent field gets.
    [Test]
    public void CheckLayoutConventionsContract_WhenReferencesSourcePaths_NoSourceRootsAtAll_EmitsUnavailableDiagnostic()
    {
        string assemblyName = typeof(LayoutConventionContractTests).Assembly.GetName().Name!;
        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, $"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{assemblyName}]
            contracts:
              strict_layout_conventions:
                - name: path-based-when-rule
                  files_matching:
                    namespace_segment: Services
                    when: subject.sourcePaths.size() > 0
                  forbid_type_kind: interface
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(((LayoutConventionPayload)violations[0].Payload!).DataUnavailable, Is.True);
    }

    // Regression: an ambiguous partial-class type whose candidate path matches the folder selector
    // must still respect `when` - if the predicate would have excluded it (e.g. wrong role/name),
    // reporting it as "cannot evaluate" would be a false positive for a type the policy was never
    // going to flag in the first place.
    [Test]
    public void CheckLayoutConventionsContract_AmbiguousPartialType_WhenExcludes_NoViolation()
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
                    when: subject.simpleName == "NotThePartialOffender"
                  forbid_type_kind: class
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("PartialOffender", StringComparison.Ordinal)), Is.False);
    }

    // Regression: the flip side of the test above - when `when` would have matched the ambiguous
    // type, it must still be reported as unresolvable.
    [Test]
    public void CheckLayoutConventionsContract_AmbiguousPartialType_WhenIncludes_IsViolation()
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
                  forbid_type_kind: class
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("PartialOffender", StringComparison.Ordinal)
            && v.Payload is LayoutConventionPayload { DataUnavailable: true }), Is.True);
    }

    // Regression: subject.sourcePaths is empty (not an evaluation error) for a candidate with no
    // resolved source file. The run-level guard only catches this when NO fact anywhere in the run
    // has a path; here OTHER fixtures ARE source-enriched, so only this specific unfiled candidate
    // must be flagged as unresolvable instead of silently excluded by the always-false predicate.
    [Test]
    public void CheckLayoutConventionsContract_WhenReferencesSourcePaths_PartialEnrichment_UnfiledCandidateIsViolation()
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
                - name: path-based-when-rule
                  files_matching:
                    namespace_segment: UnfiledNamespace
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
    }

    // Regression: require_matching_interface must only demand a counterpart for concrete classes -
    // an abstract class is itself an extension point, not a leaf implementation.
    [Test]
    public void CheckLayoutConventionsContract_RequireMatchingInterface_AbstractClass_NoViolation()
    {
        var contract = new ArchitectureLayoutConventionContract
        {
            Name = "abstract-services-require-matching-interface",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "AbstractServices" },
            RequireMatchingInterface = new ArchitectureRequireMatchingInterface { NamePrefix = "I" }
        };
        var runner = new ArchitectureContractRunner(CreateContext(), CreateDocument(contract));

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("AbstractBaseService", StringComparison.Ordinal)), Is.False);
    }
}
