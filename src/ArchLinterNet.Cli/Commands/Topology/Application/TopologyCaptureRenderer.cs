using System.Text.Json;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Topology;

namespace ArchLinterNet.Cli.Commands.Topology.Application;

/// <summary>Renders the review-only capture document without adding policy semantics.</summary>
internal static class TopologyCaptureRenderer
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
    };

    internal static string FormatJson(ArchitectureTopologyCaptureOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return JsonSerializer.Serialize(new
        {
            kind = ArchitectureTopologyCaptureOutcome.DocumentKind,
            schema_version = ArchitectureTopologyCaptureOutcome.CurrentSchemaVersion,
            subject_kind = outcome.SubjectKind,
            subjects = outcome.Subjects
                .OrderBy(subject => subject.Identity, StringComparer.Ordinal)
                .ThenBy(subject => subject.Subject, StringComparer.Ordinal)
                .Select(subject => new
                {
                    identity = subject.Identity,
                    subject_kind = subject.SubjectKind,
                    subject = subject.Subject,
                    project = subject.Project,
                    assembly = subject.Assembly,
                })
                .ToArray(),
            relationships = outcome.Relationships
                .OrderBy(relationship => relationship.SourceIdentity, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.TargetIdentity, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.Witness, StringComparer.Ordinal)
                .Select(relationship => new
                {
                    source_identity = relationship.SourceIdentity,
                    target_identity = relationship.TargetIdentity,
                    witness = relationship.Witness,
                })
                .ToArray(),
            repository_root = outcome.RepositoryRoot,
            policy_import_paths = OrderedPaths(outcome.PolicyImportPaths),
            resolved_assembly_paths = OrderedPaths(outcome.ResolvedAssemblyPaths),
            discovered_project_paths = OrderedPaths(outcome.DiscoveredProjectPaths),
            preflight_diagnostics = outcome.PreflightDiagnostics
                .OrderBy(diagnostic => diagnostic.ContractName, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.ContractId, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.State)
                .Select(FormatPreflightDiagnostic)
                .ToArray(),
            preflight_blocked = outcome.PreflightBlocked,
        }, _jsonOptions);
    }

    internal static string FormatHuman(ArchitectureTopologyCaptureOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        List<string> lines =
        [
            "Topology capture (review artifact)",
            $"Subject kind: {outcome.SubjectKind}",
            $"Subjects: {outcome.Subjects.Count}",
            $"Relationships: {outcome.Relationships.Count}",
            $"Repository root: {outcome.RepositoryRoot}",
        ];

        foreach (ArchitectureTopologyCaptureFact subject in outcome.Subjects
            .OrderBy(subject => subject.Identity, StringComparer.Ordinal))
        {
            lines.Add($"  subject: {subject.Subject} [{subject.Identity}]");
        }

        foreach (ArchitectureTopologyCaptureRelationship relationship in outcome.Relationships
            .OrderBy(relationship => relationship.SourceIdentity, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetIdentity, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Witness, StringComparer.Ordinal))
        {
            lines.Add($"  relationship: {relationship.SourceIdentity} -> {relationship.TargetIdentity} ({relationship.Witness})");
        }

        if (outcome.PreflightDiagnostics.Count > 0)
        {
            lines.Add($"Preflight blocked: {outcome.PreflightBlocked}");
            foreach (BuildStatePreflightDiagnostic diagnostic in outcome.PreflightDiagnostics
                .OrderBy(diagnostic => diagnostic.ContractName, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.ContractId, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.State))
            {
                lines.Add($"  preflight: {diagnostic.State} {diagnostic.ContractName}");
            }
        }

        lines.Add("This is review evidence; it does not modify or approve a policy.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string[] OrderedPaths(IEnumerable<string> paths) => paths
        .Where(path => path is not null)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    private static object FormatPreflightDiagnostic(BuildStatePreflightDiagnostic diagnostic)
    {
        BuildStatePreflightEvidence evidence = diagnostic.Evidence;
        return new
        {
            contract_name = diagnostic.ContractName,
            contract_id = diagnostic.ContractId,
            state = CliErrorOutputWriter.FormatPreflightState(diagnostic.State),
            project_path = evidence.ProjectPath,
            assembly_name = evidence.AssemblyName,
            requested_configuration = evidence.RequestedConfiguration,
            observed_configuration = evidence.ObservedConfiguration,
            requested_target_framework = evidence.RequestedTargetFramework,
            observed_target_framework = evidence.ObservedTargetFramework,
            expected_output_path = evidence.ExpectedOutputPath,
            searched_paths = OrderedPaths(evidence.SearchedPaths),
            build_command = evidence.BuildCommand,
            detail = evidence.Detail,
            cache_eligibility = evidence.CacheEligibility,
            cache_ineligibility_reasons = OrderedPaths(evidence.CacheIneligibilityReasons),
        };
    }

}
