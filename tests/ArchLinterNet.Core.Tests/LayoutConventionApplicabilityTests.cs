using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class LayoutConventionContractTests
{
    [Test]
    public void ApplicabilityInventory_ExpectedFolderRemoved_IsStale()
    {
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: ".",
            exhaustive: false,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "removed-services",
                Path = "RemovedServices",
                ConventionId = "services",
            });

        LayoutConventionApplicabilityChecker.Result result = EvaluateInventory(inventory, ServicesConvention());

        Assert.That(result.Records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
        Assert.That(result.Records.Single().Reasons.Select(reason => reason.Code),
            Does.Contain(ArchitectureApplicabilityReasonCodes.StaleDeclaration));
    }

    [Test]
    public void ApplicabilityInventory_ExpectedFolderContainingOnlyPartialType_IsNotStale()
    {
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: "Elsewhere",
            exhaustive: false,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "partial-type",
                Path = ".",
                ConventionId = "elsewhere",
            });
        var elsewhereConvention = new ArchitectureLayoutConventionContract
        {
            Id = "elsewhere",
            Name = "elsewhere",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Elsewhere" },
            RequireTypeKind = "class",
        };

        LayoutConventionApplicabilityChecker.Result result = EvaluateInventory(inventory, elsewhereConvention);

        Assert.Multiple(() =>
        {
            Assert.That(result.Records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
            Assert.That(result.Records.Single().Reasons, Is.Empty);
        });
    }

    [Test]
    public void ApplicabilityInventory_FileLevelNamespaceSelection_MapsEveryDeclarationInSelectedFile()
    {
        var convention = new ArchitectureLayoutConventionContract
        {
            Id = "mixed-namespace",
            Name = "mixed namespace",
            FilesMatching = new ArchitectureLayoutFileMatcher { NamespaceSegment = "MixedNamespaceFile" },
            ForbidTypeKind = "interface",
        };
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: "MixedNamespaceFile",
            exhaustive: true,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "mixed",
                Path = ".",
                ConventionId = "mixed-namespace",
            });

        LayoutConventionApplicabilityChecker.Result applicability = EvaluateInventory(inventory, convention);
        var normalRunner = new ArchitectureContractRunner(CreateContext(), CreateDocument(convention));
        List<ArchitectureViolation> normalViolations = normalRunner.Session.CheckLayoutConventionsContract(convention);

        Assert.Multiple(() =>
        {
            Assert.That(applicability.Records, Is.All.Matches<ArchitectureApplicabilityRecord>(record =>
                record.State == ArchitectureApplicabilityRecordState.Evaluable));
            Assert.That(applicability.SubjectMappings.Select(mapping => mapping.SubjectIdentity),
                Has.Some.Contains("ServiceInMatchingNamespace"));
            Assert.That(applicability.SubjectMappings.Select(mapping => mapping.SubjectIdentity),
                Has.Some.Contains("IEscapingInterface"));
            Assert.That(normalViolations.Select(violation => violation.SourceType),
                Has.Some.Contains("IEscapingInterface"),
                "The ordinary checker and inventory must use the same file-level candidate set.");
        });
    }

    [Test]
    public void ApplicabilityInventory_NamespaceExclusion_UsesFileLevelCandidateSet()
    {
        var convention = new ArchitectureLayoutConventionContract
        {
            Id = "exclude-mixed-namespace",
            Name = "exclude mixed namespace",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "MixedNamespaceFile" },
            ExcludeFilesMatching =
            {
                new ArchitectureLayoutFileMatcher { NamespaceSegment = "MixedNamespaceFileOther" },
            },
            ForbidTypeKind = "interface",
        };
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: "MixedNamespaceFile",
            exhaustive: false,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "mixed",
                Path = ".",
                ConventionId = "exclude-mixed-namespace",
            });

        LayoutConventionApplicabilityChecker.Result applicability = EvaluateInventory(inventory, convention);
        var normalRunner = new ArchitectureContractRunner(CreateContext(), CreateDocument(convention));
        List<ArchitectureViolation> normalViolations = normalRunner.Session.CheckLayoutConventionsContract(convention);

        Assert.Multiple(() =>
        {
            Assert.That(applicability.Records.Single().Reasons.Select(reason => reason.Code),
                Does.Contain(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput));
            Assert.That(normalViolations, Is.Empty,
                "A namespace exclusion selects the physical file and removes all of its declarations.");
        });
    }

    [Test]
    public void ApplicabilityInventory_IncludeExcludeWhen_UsesTheOrdinaryCheckerProjection()
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
                - id: mixed-with-when
                  name: mixed with when
                  files_matching:
                    folder_segment: MixedNamespaceFile
                  exclude_files_matching:
                    - namespace_segment: MixedNamespaceFileOther
                      when: subject.simpleName == "ServiceInMatchingNamespace"
                  forbid_type_kind: class
              strict_layout_convention_applicability:
                - id: mixed-with-when-inventory
                  name: mixed with when inventory
                  scope: MixedNamespaceFile
                  exhaustive: true
                  expected_folders:
                    - id: mixed
                      path: .
                      convention_id: mixed-with-when
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        ArchitectureLayoutConventionContract convention = document.Contracts.StrictLayoutConventions.Single();
        ArchitectureLayoutConventionApplicabilityContract inventory =
            document.Contracts.StrictLayoutConventionApplicability.Single();
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        LayoutConventionApplicabilityChecker.Result applicability = LayoutConventionApplicabilityChecker.Evaluate(
            runner.Session.CheckerContext,
            inventory,
            document.Contracts.StrictLayoutConventions);
        List<ArchitectureViolation> normalViolations = runner.Session.CheckLayoutConventionsContract(convention);

        Assert.Multiple(() =>
        {
            Assert.That(applicability.SubjectMappings.Select(mapping => mapping.SubjectIdentity),
                Has.Some.Contains("IEscapingInterface"));
            Assert.That(applicability.SubjectMappings.Select(mapping => mapping.SubjectIdentity),
                Has.None.Contains("ServiceInMatchingNamespace"));
            Assert.That(normalViolations, Is.Empty,
                "The exclusion's file-level namespace selector is refined by its when predicate.");
        });
    }

    [Test]
    public void ApplicabilityInventory_ExhaustiveScope_UnmappedFolderIsUnassessable()
    {
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: "Services",
            exhaustive: true,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "only-order-service",
                Path = "OrderOnly",
                ConventionId = "services",
            });

        LayoutConventionApplicabilityChecker.Result result = EvaluateInventory(inventory, ServicesConvention());

        Assert.That(result.Records.Single(record => record.Reasons.Any(reason =>
                reason.Code == ArchitectureApplicabilityReasonCodes.UnmappedSubject)).State,
            Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
        Assert.That(result.Records.SelectMany(record => record.Reasons).Select(reason => reason.Code),
            Does.Contain(ArchitectureApplicabilityReasonCodes.UnmappedSubject));
    }

    [Test]
    public void ApplicabilityInventory_OverlappingExpectedFolders_IsAmbiguous()
    {
        var alternateConvention = new ArchitectureLayoutConventionContract
        {
            Id = "services-alternate",
            Name = "services alternate",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
            RequireTypeKind = "class",
        };
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: ".",
            exhaustive: true,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "services-one",
                Path = "Services",
                ConventionId = "services",
            },
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "services-two",
                Path = "Services",
                ConventionId = "services-alternate",
            });

        LayoutConventionApplicabilityChecker.Result result = EvaluateInventory(
            inventory,
            ServicesConvention(),
            alternateConvention);

        Assert.That(result.Records.Single(record => record.Reasons.Any(reason =>
                reason.Code == ArchitectureApplicabilityReasonCodes.AmbiguousSubject)).State,
            Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
        Assert.That(result.Records.SelectMany(record => record.Reasons).Select(reason => reason.Code),
            Does.Contain(ArchitectureApplicabilityReasonCodes.AmbiguousSubject));
    }

    [Test]
    public void ApplicabilityInventory_OverlappingFoldersForSameConvention_IsNotAmbiguous()
    {
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: ".",
            exhaustive: true,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "services-one",
                Path = "Services",
                ConventionId = "services",
            },
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "services-two",
                Path = "Services",
                ConventionId = "services",
            });

        LayoutConventionApplicabilityChecker.Result result = EvaluateInventory(inventory, ServicesConvention());

        Assert.That(result.Records.SelectMany(record => record.Reasons).Select(reason => reason.Code),
            Does.Not.Contain(ArchitectureApplicabilityReasonCodes.AmbiguousSubject));
    }

    [Test]
    public void ApplicabilityInventory_LinkedSelectorMatchesZero_IsUnexpectedEmpty()
    {
        var driftedConvention = new ArchitectureLayoutConventionContract
        {
            Id = "renamed-services-selector",
            Name = "renamed services selector",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "RenamedServices" },
            RequireTypeKind = "class",
        };
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: ".",
            exhaustive: false,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "services",
                Path = "Services",
                ConventionId = "renamed-services-selector",
            });

        LayoutConventionApplicabilityChecker.Result result = EvaluateInventory(inventory, driftedConvention);

        Assert.That(result.Records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
        Assert.That(result.Records.Single().Reasons.Select(reason => reason.Code),
            Does.Contain(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput));
    }

    [Test]
    public void ApplicabilityInventory_OutsideScope_DoesNotCreateUnmappedEvidence()
    {
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: "Services",
            exhaustive: true,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "services",
                Path = ".",
                ConventionId = "services",
            });

        LayoutConventionApplicabilityChecker.Result result = EvaluateInventory(inventory, ServicesConvention());

        Assert.That(result.Records, Is.All.Matches<ArchitectureApplicabilityRecord>(record =>
            record.State == ArchitectureApplicabilityRecordState.Evaluable));
        Assert.That(result.Records.SelectMany(record => record.Reasons), Is.Empty);
    }

    [Test]
    public void Executor_StrictInventory_ProjectsStaleFolderIntoItsBaselineGroup()
    {
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: ".",
            exhaustive: false,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "removed-services",
                Path = "RemovedServices",
                ConventionId = "services",
            });
        ArchitectureContractDocument document = CreateInventoryDocument(inventory, ServicesConvention());
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        ArchitectureContractExecutionResult execution = new ArchitectureContractExecutor()
            .Execute(runner.Session, "strict", new ArchitectureContractHandlerRegistry());
        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            execution.ApplicabilityExpectedEntries,
            execution.ApplicabilityRecords,
            conformancePassed: true)!;
        ArchitectureApplicabilityProjection projection = ArchitectureApplicabilityProjector.Project(completion, "strict")!;
        ArchitectureBaselineCandidate candidate = ArchitectureApplicabilityBaselineCandidateProjector
            .Project(document, "strict", projection)
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(execution.ApplicabilityExpectedEntries.Single().Family,
                Is.EqualTo("layout_convention_applicability"));
            Assert.That(execution.ApplicabilityExpectedEntries.Single().ControlIdentity,
                Is.EqualTo("layout-folder-inventory/removed-services:services"));
            Assert.That(execution.ApplicabilityRecords.Single().Reasons.Single().Code,
                Is.EqualTo(ArchitectureApplicabilityReasonCodes.StaleDeclaration));
            Assert.That(projection.Findings.Single().Identity!.ContractFamily,
                Is.EqualTo("layout_convention_applicability"));
            Assert.That(candidate.ContractGroup, Is.EqualTo("strict_layout_convention_applicability"));
            Assert.That(candidate.ContractId, Is.EqualTo("layout-folder-inventory"));
        });
    }

    [Test]
    public void Executor_AuditInventory_IsAbsentFromStrictAndProjectsItsAuditBaselineGroup()
    {
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: ".",
            exhaustive: false,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "removed-services",
                Path = "RemovedServices",
                ConventionId = "services",
            });
        ArchitectureContractDocument document = CreateInventoryDocument(inventory, ServicesConvention());
        document.Contracts.StrictLayoutConventions.Clear();
        document.Contracts.StrictLayoutConventionApplicability.Clear();
        document.Contracts.AuditLayoutConventions = [ServicesConvention()];
        document.Contracts.AuditLayoutConventionApplicability = [inventory];
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        var executor = new ArchitectureContractExecutor();

        ArchitectureContractExecutionResult strict = executor.Execute(
            runner.Session, "strict", new ArchitectureContractHandlerRegistry());
        ArchitectureContractExecutionResult audit = executor.Execute(
            runner.Session, "audit", new ArchitectureContractHandlerRegistry());
        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            audit.ApplicabilityExpectedEntries,
            audit.ApplicabilityRecords,
            conformancePassed: true)!;
        ArchitectureApplicabilityProjection projection = ArchitectureApplicabilityProjector.Project(completion, "audit")!;
        ArchitectureBaselineCandidate candidate = ArchitectureApplicabilityBaselineCandidateProjector
            .Project(document, "audit", projection)
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(strict.ApplicabilityExpectedEntries, Is.Empty);
            Assert.That(strict.ApplicabilityRecords, Is.Empty);
            Assert.That(audit.ApplicabilityRecords.Single().Reasons.Single().Code,
                Is.EqualTo(ArchitectureApplicabilityReasonCodes.StaleDeclaration));
            Assert.That(projection.Findings.Single().Mode, Is.EqualTo("audit"));
            Assert.That(candidate.ContractGroup, Is.EqualTo("audit_layout_convention_applicability"));
        });
    }

    private LayoutConventionApplicabilityChecker.Result EvaluateInventory(
        ArchitectureLayoutConventionApplicabilityContract inventory,
        params ArchitectureLayoutConventionContract[] conventions)
    {
        ArchitectureContractDocument document = CreateInventoryDocument(inventory, conventions);
        var runner = new ArchitectureContractRunner(CreateContext(), document);
        return LayoutConventionApplicabilityChecker.Evaluate(runner.Session.CheckerContext, inventory, conventions);
    }

    private static ArchitectureContractDocument CreateInventoryDocument(
        ArchitectureLayoutConventionApplicabilityContract inventory,
        params ArchitectureLayoutConventionContract[] conventions) => new()
        {
            Version = 1,
            Name = "layout-applicability",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(LayoutConventionContractTests).Assembly.GetName().Name! },
                SourceRoots = new List<string> { "." },
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictLayoutConventions = conventions.ToList(),
                StrictLayoutConventionApplicability = new List<ArchitectureLayoutConventionApplicabilityContract> { inventory },
            },
        };

    private static ArchitectureLayoutConventionApplicabilityContract CreateInventory(
        string scope,
        bool exhaustive,
        params ArchitectureLayoutConventionExpectedFolder[] expectedFolders) => new()
        {
            Id = "layout-folder-inventory",
            Name = "layout folder inventory",
            Scope = scope,
            Exhaustive = exhaustive,
            ExpectedFolders = expectedFolders.ToList(),
        };

    private static ArchitectureLayoutConventionContract ServicesConvention() => new()
    {
        Id = "services",
        Name = "services",
        FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Services" },
        RequireTypeKind = "class",
    };
}
