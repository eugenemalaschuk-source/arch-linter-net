using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    /// <summary>Renders the evaluated lifecycle state of every manual architecture waiver.</summary>
    public string FormatWaiversForHumans(IReadOnlyCollection<ArchitectureWaiverLifecycleRecord> waivers)
    {
        if (waivers.Count == 0)
        {
            return string.Empty;
        }

        return "Architecture waivers:" + Environment.NewLine
            + string.Join(Environment.NewLine, waivers
                .OrderBy(waiver => waiver.State, StringComparer.Ordinal)
                .ThenBy(waiver => waiver.Id, StringComparer.Ordinal)
                .Select(waiver =>
                    $"  [{waiver.State}] {waiver.Id}: {waiver.ContractName} " +
                    $"({waiver.SourceType} -> {waiver.ForbiddenReference})" +
                    $"; owner: {waiver.Owner ?? "?"}; issue: {waiver.Issue ?? "?"}" +
                    $"; introduced: {FormatDate(waiver.Introduced)}; expires: {FormatDate(waiver.Expires)}" +
                    (waiver.PolicyLocation is null
                        ? string.Empty
                        : $" (policy: {waiver.PolicyLocation.SourcePath}:{waiver.PolicyLocation.YamlPath})")));
    }

    /// <summary>
    /// Adds the normalized waiver lifecycle collection to a complete CI-artifact JSON document.
    /// The method is deliberately additive so existing formatter overloads remain source compatible.
    /// </summary>
    public static string AddWaiversToCiArtifacts(
        string ciArtifacts,
        IReadOnlyCollection<ArchitectureWaiverLifecycleRecord> waivers)
    {
        ArgumentNullException.ThrowIfNull(ciArtifacts);
        ArgumentNullException.ThrowIfNull(waivers);

        JsonNode? parsed = JsonNode.Parse(ciArtifacts);
        if (parsed is not JsonObject payload)
        {
            throw new InvalidOperationException("CI artifact output must be a JSON object before waiver data can be added.");
        }

        payload["waivers"] = JsonSerializer.SerializeToNode(waivers
            .OrderBy(waiver => waiver.Id, StringComparer.Ordinal)
            .ThenBy(waiver => waiver.ContractName, StringComparer.Ordinal)
            .Select(waiver => new
            {
                id = waiver.Id,
                state = waiver.State,
                contract = waiver.ContractName,
                contract_id = waiver.ContractId,
                contract_group = waiver.ContractGroup,
                source_type = waiver.SourceType,
                forbidden_reference = waiver.ForbiddenReference,
                target_fingerprint = waiver.TargetFingerprint,
                reason = waiver.Reason,
                owner = waiver.Owner,
                issue = waiver.Issue,
                introduced = FormatDate(waiver.Introduced),
                expires = FormatDate(waiver.Expires),
                evaluation_date = waiver.EvaluationDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                matches_governed_finding = waiver.MatchesGovernedFinding,
                policy_location = waiver.PolicyLocation is null
                    ? null
                    : FormatPolicyLocationForJson(waiver.PolicyLocation),
            }));

        return payload.ToJsonString();
    }

    private static string? FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
