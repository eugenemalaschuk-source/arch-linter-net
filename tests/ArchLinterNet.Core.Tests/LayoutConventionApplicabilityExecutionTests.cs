using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class LayoutConventionContractTests
{
    [Test]
    public void Executor_ApplicabilityInventoryNotSelected_ProducesNoEvidenceOrFindings()
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
        var selectedContract = new ArchitectureLayoutConventionContract
        {
            Id = "some-other-contract",
            Name = "some other contract",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "NoSuchFolder" },
            RequireTypeKind = "class",
        };
        ArchitectureContractDocument document = CreateInventoryDocument(
            inventory,
            ServicesConvention(),
            selectedContract);
        var runner = new ArchitectureContractRunner(
            CreateContext(),
            document,
            new HashSet<string>(StringComparer.Ordinal) { "some-other-contract" });

        ArchitectureContractExecutionResult execution = new ArchitectureContractExecutor()
            .Execute(runner.Session, "strict", new ArchitectureContractHandlerRegistry());

        Assert.Multiple(() =>
        {
            Assert.That(execution.ApplicabilityExpectedEntries, Is.Empty);
            Assert.That(execution.ApplicabilityRecords, Is.Empty);
            Assert.That(execution.Violations, Is.Empty);
            Assert.That(runner.BaselineCandidates, Is.Empty);
        });
    }

    [Test]
    public void Executor_ExhaustiveInventory_ReportsSubjectDiagnosticsAndBaselinesEachSubjectIndependently()
    {
        WriteFixtureFile("Unknown/UnknownA/OrderService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public sealed class OrderService { } }");
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: "Unknown",
            exhaustive: true,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "mapped",
                Path = "Mapped",
                ConventionId = "never-matches",
            });
        var convention = new ArchitectureLayoutConventionContract
        {
            Id = "never-matches",
            Name = "never matches",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Mapped" },
            RequireTypeKind = "class",
        };
        ArchitectureContractDocument document = CreateInventoryDocument(inventory, convention);

        var firstRunner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContractExecutionResult firstExecution = new ArchitectureContractExecutor()
            .Execute(firstRunner.Session, "strict", new ArchitectureContractHandlerRegistry());
        ArchitectureBaselineDocument baseline = new ArchitectureBaselineGenerator().Generate(
            document,
            firstRunner.BaselineCandidates,
            "known UnknownA debt");

        WriteFixtureFile("Unknown/UnknownB/PaymentService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public sealed class PaymentService { } }");
        var secondRunner = new ArchitectureContractRunner(CreateContext(), document);
        ArchitectureContractExecutionResult secondExecution = new ArchitectureContractExecutor()
            .Execute(secondRunner.Session, "strict", new ArchitectureContractHandlerRegistry());
        ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
            document,
            baseline,
            secondRunner.BaselineCandidates,
            "strict");

        Assert.Multiple(() =>
        {
            Assert.That(firstExecution.Violations.Select(violation => violation.SourceType),
                Has.Some.Contains("Unknown/UnknownA"));
            Assert.That(secondExecution.Violations.Select(violation => violation.SourceType),
                Has.Some.Contains("Unknown/UnknownB"));
            Assert.That(comparison.Frozen.Select(entry => entry.SourceType),
                Has.Some.Contains("Unknown/UnknownA"));
            Assert.That(comparison.New.Select(entry => entry.SourceType),
                Has.Some.Contains("Unknown/UnknownB"),
                "A new observed subject must remain new debt even when another subject is baselined.");
        });
    }

    [Test]
    public void Executor_ExhaustiveInventory_ReportsEachAmbiguousSubjectIndependently()
    {
        WriteFixtureFile("Ambiguous/First/OrderService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public sealed class OrderService { } }");
        WriteFixtureFile("Ambiguous/Second/PaymentService.cs",
            "namespace LayoutConventionContractTestFixtures.Services { public sealed class PaymentService { } }");
        var firstConvention = new ArchitectureLayoutConventionContract
        {
            Id = "ambiguous-one",
            Name = "ambiguous one",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Ambiguous" },
            RequireTypeKind = "class",
        };
        var secondConvention = new ArchitectureLayoutConventionContract
        {
            Id = "ambiguous-two",
            Name = "ambiguous two",
            FilesMatching = new ArchitectureLayoutFileMatcher { FolderSegment = "Ambiguous" },
            RequireTypeKind = "class",
        };
        ArchitectureLayoutConventionApplicabilityContract inventory = CreateInventory(
            scope: "Ambiguous",
            exhaustive: true,
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "first",
                Path = ".",
                ConventionId = "ambiguous-one",
            },
            new ArchitectureLayoutConventionExpectedFolder
            {
                Id = "second",
                Path = ".",
                ConventionId = "ambiguous-two",
            });
        ArchitectureContractDocument document = CreateInventoryDocument(inventory, firstConvention, secondConvention);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        ArchitectureContractExecutionResult execution = new ArchitectureContractExecutor()
            .Execute(runner.Session, "strict", new ArchitectureContractHandlerRegistry());
        ArchitectureViolation[] ambiguousViolations = execution.Violations
            .Where(violation => violation.ForbiddenReferences.Contains(
                ArchitectureApplicabilityReasonCodes.AmbiguousSubject))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(ambiguousViolations, Has.Length.EqualTo(2));
            Assert.That(ambiguousViolations.Select(violation => violation.SourceType),
                Has.Some.Contains("Ambiguous/First"));
            Assert.That(ambiguousViolations.Select(violation => violation.SourceType),
                Has.Some.Contains("Ambiguous/Second"));
        });
    }
}
