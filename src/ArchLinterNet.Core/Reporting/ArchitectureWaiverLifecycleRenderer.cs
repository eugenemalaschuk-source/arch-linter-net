using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Renders the canonical waiver lifecycle evidence shared by report formats.</summary>
internal static class ArchitectureWaiverLifecycleRenderer
{
    internal static string RenderForHumans(IReadOnlyCollection<ArchitectureWaiverLifecycleRecord> waivers)
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
                    $"  [{waiver.State}] {waiver.Id}: {waiver.ContractName} "
                    + $"({waiver.SourceType} -> {waiver.ForbiddenReference})"
                    + $"; target: {waiver.TargetFingerprint ?? "?"}; reason: {waiver.Reason}"
                    + $"; owner: {waiver.Owner ?? "?"}; issue: {waiver.Issue ?? "?"}"
                    + $"; introduced: {FormatDate(waiver.Introduced)}; expires: {FormatDate(waiver.Expires)}"
                    + (waiver.PolicyLocation is null
                        ? string.Empty
                        : $" (policy: {waiver.PolicyLocation.SourcePath}:{waiver.PolicyLocation.YamlPath})")));
    }

    internal static string AddToCiArtifacts(
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

        payload["waivers"] = new JsonArray(waivers
            .OrderBy(waiver => waiver.Id, StringComparer.Ordinal)
            .ThenBy(waiver => waiver.ContractName, StringComparer.Ordinal)
            .Select(FormatWaiverForJson)
            .ToArray());

        return payload.ToJsonString();
    }

    internal static JsonNode FormatWaiverForJson(ArchitectureWaiverLifecycleRecord waiver) => new JsonObject
    {
        ["id"] = waiver.Id,
        ["state"] = waiver.State,
        ["contract"] = waiver.ContractName,
        ["contract_id"] = waiver.ContractId,
        ["contract_group"] = waiver.ContractGroup,
        ["source_type"] = waiver.SourceType,
        ["forbidden_reference"] = waiver.ForbiddenReference,
        ["target_fingerprint"] = waiver.TargetFingerprint,
        ["reason"] = waiver.Reason,
        ["owner"] = waiver.Owner,
        ["issue"] = waiver.Issue,
        ["introduced"] = FormatDate(waiver.Introduced),
        ["expires"] = FormatDate(waiver.Expires),
        ["evaluation_date"] = waiver.EvaluationDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ["matches_governed_finding"] = waiver.MatchesGovernedFinding,
        ["policy_location"] = waiver.PolicyLocation is null
            ? null
            : System.Text.Json.JsonSerializer.SerializeToNode(ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson(waiver.PolicyLocation)),
    };

    internal static string? FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
