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
