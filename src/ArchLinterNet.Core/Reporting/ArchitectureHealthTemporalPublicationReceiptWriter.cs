using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Stable JSON projection for the trusted temporal publication receipt.</summary>
internal static class ArchitectureHealthTemporalPublicationReceiptWriter
{
    internal static string Format(ArchitectureHealthTemporalPublicationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var reasons = new JsonArray();
        foreach (ArchitectureHealthTemporalPublicationReceiptReason reason in receipt.Reasons)
        {
            reasons.Add(new JsonObject
            {
                ["code"] = reason.Code,
                ["detail"] = reason.Detail,
            });
        }

        return new JsonObject
        {
            ["schema_id"] = ArchitectureHealthTemporalPublicationReceipt.CurrentSchemaId,
            ["state"] = receipt.State == ArchitectureHealthTemporalPublicationReceiptState.Ready
                ? "ready"
                : "unassessable",
            ["evaluation_date"] = receipt.EvaluationDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["semantic_horizon"] = receipt.SemanticHorizon?.ToUniversalTime().ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
            ["source_health_sha256"] = receipt.SourceHealthSha256,
            ["badge_payload_sha256"] = receipt.BadgePayloadSha256,
            ["merged_tree_sha"] = receipt.MergedTreeSha,
            ["producer_identity_sha256"] = receipt.ProducerIdentitySha256,
            ["reasons"] = reasons,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}

