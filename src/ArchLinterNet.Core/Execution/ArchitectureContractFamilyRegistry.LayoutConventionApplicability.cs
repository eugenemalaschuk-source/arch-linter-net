using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

internal static class LayoutConventionApplicabilityContractHandler
{
    internal static ArchitectureHandlerResult Check(
        ArchitectureAnalysisSession session,
        IArchitectureContract contract)
    {
        var inventory = (ArchitectureLayoutConventionApplicabilityContract)contract;
        if (!session.IsContractSelected(inventory))
        {
            return new ArchitectureHandlerResult(Array.Empty<ArchitectureViolation>(), Array.Empty<string>());
        }

        bool strict = string.Equals(
            session.Catalog.ResolveGroup(inventory),
            "strict_layout_convention_applicability",
            StringComparison.Ordinal);
        IReadOnlyList<ArchitectureLayoutConventionContract> conventions = strict
            ? session.Document.Contracts.StrictLayoutConventions
            : session.Document.Contracts.AuditLayoutConventions;
        LayoutConventionApplicabilityChecker.Result result = LayoutConventionApplicabilityChecker.Evaluate(
            session.CheckerContext,
            inventory,
            conventions);
        ArchitectureContractExecutionContext executionContext = session.CreateExecutionContext(
            inventory,
            Array.Empty<ArchitectureIgnoredViolation>());
        string inventoryIdentity = inventory.Id ?? inventory.Name;
        List<ArchitectureViolation> subjectViolations = result.SubjectIssues
            .Where(issue => !executionContext.IsIgnored(
                issue.SubjectIdentity,
                issue.ReasonCode,
                sourceMember: LayoutConventionApplicabilityChecker.Family,
                targetMember: issue.ReasonCode,
                configuration: inventoryIdentity))
            .Select(issue => CreateSubjectViolation(inventory, issue, inventoryIdentity))
            .ToList();

        return new ArchitectureHandlerResult(subjectViolations, Array.Empty<string>())
        {
            ApplicabilityExpectedEntries = result.ExpectedEntries,
            ApplicabilityRecords = result.Records,
        };
    }

    private static ArchitectureViolation CreateSubjectViolation(
        ArchitectureLayoutConventionApplicabilityContract inventory,
        LayoutConventionApplicabilityChecker.SubjectIssue issue,
        string inventoryIdentity)
    {
        ArchitectureApplicabilityProvenance provenance = new(
            LayoutConventionApplicabilityChecker.Family,
            issue.SubjectIdentity,
            inventoryIdentity);
        return new ArchitectureViolation(
            inventory.Name,
            inventory.Id,
            issue.SubjectIdentity,
            "layout convention applicability",
            [issue.ReasonCode])
        {
            Payload = new ArchitectureLayoutConventionApplicabilitySubjectPayload(
                issue.SubjectIdentity,
                issue.ReasonCode,
                provenance),
            Identity = new ArchitectureViolationIdentity(
                ArchitectureViolationIdentity.CurrentVersion,
                LayoutConventionApplicabilityChecker.Family,
                ArchitectureViolationIdentity.ResolveKind(LayoutConventionApplicabilityChecker.Family),
                inventoryIdentity,
                SourceAssembly: null,
                SourceType: issue.SubjectIdentity,
                SourceMember: LayoutConventionApplicabilityChecker.Family,
                TargetAssembly: null,
                TargetType: null,
                TargetMember: issue.ReasonCode,
                Occurrence: 0,
                Configuration: inventoryIdentity),
        };
    }
}
