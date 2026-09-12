using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Projects canonical Health/change artifacts without re-evaluating their authorities.</summary>
public static class ArchitecturePrReportProjector
{
    /// <summary>Creates the typed projection consumed by a presentation adapter.</summary>
    public static ArchitecturePrReportProjection Project(ArchitecturePrReportInput input) =>
        Project(input, input?.NavigationContext);

    /// <summary>
    /// Creates the typed projection with optional producer-supplied transport navigation metadata.
    /// The metadata is not evidence and cannot alter the canonical Gate, Health, or availability.
    /// </summary>
    public static ArchitecturePrReportProjection Project(
        ArchitecturePrReportInput input,
        ArchitecturePrReportNavigationContext? navigationContext)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArchitecturePrReportAvailability availability = ResolveAvailability(input);
        ArchitecturePrReportHeadline headline = new(
            input.Summary.Gate,
            input.Summary.Health,
            availability,
            input.Summary.Dimensions)
        {
            DimensionExplanations = BuildDimensionExplanations(input.Summary.Dimensions),
        };

        ArchitecturePrReportNavigationContext? safeNavigationContext =
            ArchitecturePrReportNavigationContext.TryNormalize(navigationContext,
                out ArchitecturePrReportNavigationContext? normalized)
                ? normalized
                : null;
        return new ArchitecturePrReportProjection(
            headline,
            input.Evidence,
            input.Change,
            BuildNavigation(input))
        {
            NavigationContext = safeNavigationContext,
        };
    }

    /// <summary>Reads and projects canonical local Health and change artifacts in one call.</summary>
    public static ArchitecturePrReportProjection ReadAndProject(string healthJson, string changeJson) =>
        Project(ArchitecturePrReportReader.Read(healthJson, changeJson));

    /// <summary>Reads and projects canonical artifacts with optional transport navigation metadata.</summary>
    public static ArchitecturePrReportProjection ReadAndProject(
        string healthJson,
        string changeJson,
        ArchitecturePrReportNavigationContext? navigationContext) =>
        Project(ArchitecturePrReportReader.Read(healthJson, changeJson), navigationContext);

    private static IReadOnlyList<ArchitecturePrReportDimensionExplanation> BuildDimensionExplanations(
        IReadOnlyList<ArchitectureHealthDimension> dimensions) =>
        dimensions
            .Where(dimension => dimension.State is not ArchitectureHealthDimensionState.Pass
                and not ArchitectureHealthDimensionState.NotConfigured
                and not ArchitectureHealthDimensionState.NotApplicable)
            .SelectMany(dimension => dimension.Reasons.Select(reason =>
                new ArchitecturePrReportDimensionExplanation(
                    dimension.Name,
                    dimension.State,
                    reason,
                    dimension.State is ArchitectureHealthDimensionState.Fail
                        or ArchitectureHealthDimensionState.Unassessable)))
            .OrderBy(explanation => explanation.Dimension, StringComparer.Ordinal)
            .ThenBy(explanation => explanation.State)
            .ThenBy(explanation => explanation.Code, StringComparer.Ordinal)
            .ThenBy(explanation => explanation.Source, StringComparer.Ordinal)
            .ThenBy(explanation => explanation.Family, StringComparer.Ordinal)
            .ThenBy(explanation => explanation.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(explanation => explanation.PolicyIdentity, StringComparer.Ordinal)
            .ThenBy(explanation => explanation.EvidenceIdentity, StringComparer.Ordinal)
            .ToArray();

    private static ArchitecturePrReportAvailability ResolveAvailability(ArchitecturePrReportInput input)
    {
        if (input.Evidence is null)
        {
            return ArchitecturePrReportAvailability.Unavailable;
        }

        if (input.Summary.Health == ArchitectureHealthState.Unassessable)
        {
            return ArchitecturePrReportAvailability.Unassessable;
        }

        bool unavailable = input.Evidence.ValidationOutcomes.Any(receipt => !IsAvailableReceipt(receipt));
        return unavailable
            ? ArchitecturePrReportAvailability.Unavailable
            : ArchitecturePrReportAvailability.Complete;
    }

    private static bool IsAvailableReceipt(ArchitecturePrReportValidationReceipt receipt)
    {
        IReadOnlyDictionary<string, string> availability = receipt.Availability;
        string[] expectedKeys =
        [
            "applicability",
            "external_evidence",
            "findings",
            "policy_inventory",
            "topology",
            "waiver_lifecycle",
        ];
        if (availability.Count != expectedKeys.Length || !expectedKeys.All(availability.ContainsKey))
        {
            return false;
        }

        if (receipt.ExternalEvidence is { } external && !external.HasCompleteTrustReceipts)
        {
            return false;
        }

        bool topology = receipt.Applicability?.Controls.Any(control => control.Record?.Topology is not null) == true;
        return Matches(availability, "policy_inventory", receipt.PolicyInventory is not null, "unavailable")
            && Matches(availability, "waiver_lifecycle", receipt.WaiverLifecycle is not null, "unavailable")
            && Matches(availability, "applicability", receipt.Applicability is not null, "unavailable")
            && Matches(availability, "topology", topology, "not_configured")
            && Matches(availability, "external_evidence", receipt.ExternalEvidence is not null, "not_configured")
            && Matches(availability, "findings", receipt.Findings is not null, "unavailable");
    }

    private static bool Matches(
        IReadOnlyDictionary<string, string> availability,
        string key,
        bool hasPayload,
        string absentValue) => availability.TryGetValue(key, out string? value)
            && (value == "available" || value == absentValue)
            && hasPayload == string.Equals(value, "available", StringComparison.Ordinal);

    private static IReadOnlyList<ArchitecturePrReportNavigationReference> BuildNavigation(
        ArchitecturePrReportInput input)
    {
        var references = new List<ArchitecturePrReportNavigationReference>
        {
            new("health", ArchitectureHealthSummary.CurrentSchemaId, null),
            new("change", ArchitectureChangeReport.ReportKind, null),
        };
        if (input.Evidence is not null)
        {
            foreach (ArchitecturePrReportValidationReceipt receipt in input.Evidence.ValidationOutcomes)
            {
                AddProvenance(references, receipt.Provenance);
                if (receipt.PolicyInventory is not null)
                {
                    references.Add(new("policy_inventory", receipt.PolicyInventory.SchemaId, null));
                    foreach (ArchitectureWaiverLifecycleRecord waiver in receipt.PolicyInventory.Waivers)
                    {
                        references.Add(new("waiver", waiver.Id, PolicyPath(waiver.PolicyLocation)));
                    }
                }

                if (receipt.WaiverLifecycle is not null)
                {
                    foreach (ArchitectureWaiverLifecycleRecord waiver in receipt.WaiverLifecycle.Records)
                    {
                        references.Add(new("waiver", waiver.Id, PolicyPath(waiver.PolicyLocation)));
                    }
                }

                if (receipt.Applicability is not null)
                {
                    foreach (ArchitecturePrReportApplicabilityControl control in receipt.Applicability.Controls)
                    {
                        references.Add(new("applicability", control.ControlIdentity, null));
                        if (control.Record?.Topology is not null)
                        {
                            foreach (ArchitecturePrReportTopologySubject subject in control.Record.Topology.Subjects)
                            {
                                references.Add(new("topology", subject.Identity, null));
                            }
                        }
                    }
                }

                foreach (ArchitecturePrReportFinding finding in receipt.Findings)
                {
                    references.Add(new("finding", finding.CanonicalIdentity, finding.SourceLocation?.Path));
                }

                if (receipt.ExternalEvidence is not null)
                {
                    foreach (ArchitecturePrReportExternalRequirement requirement in receipt.ExternalEvidence.Requirements)
                    {
                        references.Add(new("external_evidence", requirement.Id, null));
                    }

                    foreach (ArchitecturePrReportExternalEvidenceTrustReceipt trust in receipt.ExternalEvidence.TrustReceipts)
                    {
                        references.Add(new("external_evidence", trust.LogicalId, trust.ArtifactPath));
                    }
                }
            }

            AddDebtNavigation(references, input.Evidence.DebtGate);
        }

        foreach (ArchitectureChangeEntry entry in input.Change.Added.Concat(input.Change.Removed))
        {
            references.Add(new("change_surface", entry.Identity, null));
        }

        foreach (ArchitectureChangeFinding finding in input.Change.NewFindings
            .Concat(input.Change.ExistingFindings)
            .Concat(input.Change.ResolvedFindings))
        {
            references.Add(new("change_finding", finding.Identity, null));
        }

        return references
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Identity))
            .Distinct()
            .OrderBy(reference => reference.Authority, StringComparer.Ordinal)
            .ThenBy(reference => reference.Identity, StringComparer.Ordinal)
            .ThenBy(reference => reference.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddDebtNavigation(
        ICollection<ArchitecturePrReportNavigationReference> references,
        ArchitecturePrReportDebtGateReceipt debtGate)
    {
        foreach (ArchitecturePrReportBaselineEntry entry in debtGate.PersistentDebt.Entries)
        {
            references.Add(new("baseline", entry.Identity, null));
        }

        if (debtGate.PolicyWeakening is not null)
        {
            foreach (ArchitecturePrReportPolicyWeakeningFinding finding in debtGate.PolicyWeakening.Findings)
            {
                references.Add(new("policy_weakening", finding.Identity, null));
            }
        }
    }

    private static void AddProvenance(
        ICollection<ArchitecturePrReportNavigationReference> references,
        ArchitecturePrReportProvenance provenance)
    {
        references.Add(new("repository", provenance.RepositoryRoot, provenance.RepositoryRoot));
        foreach (string path in provenance.PolicyImportPaths)
        {
            references.Add(new("policy", path, path));
        }
    }

    private static string? PolicyPath(ArchitecturePolicySourceLocation? location) =>
        location is null ? null : $"{location.SourcePath}:{location.YamlPath}";
}
