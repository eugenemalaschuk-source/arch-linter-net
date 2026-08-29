using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed partial class SarifEvidenceReader
{
    private static ContextReadOutcome ReadContext(
        JsonElement run,
        SarifEvidenceArtifactReference artifact,
        out SarifEvidenceTrustStatus? status,
        out string? detail)
    {
        status = null;
        detail = null;
        string? repository = null;
        string? revision = null;
        bool conflict = false;

        if (run.TryGetProperty("versionControlProvenance", out JsonElement provenance))
        {
            if (provenance.ValueKind != JsonValueKind.Array)
            {
                status = SarifEvidenceTrustStatus.UnsupportedShape;
                detail = "The SARIF versionControlProvenance member must be an array.";
                return new ContextReadOutcome(new SarifEvidenceResolvedContext(artifact.LogicalId, null, null, null));
            }

            foreach (JsonElement entry in provenance.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    status = SarifEvidenceTrustStatus.UnsupportedShape;
                    detail = "Every SARIF version-control provenance entry must be an object.";
                    return new ContextReadOutcome(new SarifEvidenceResolvedContext(artifact.LogicalId, repository, revision, null));
                }

                if (!TryReadOptionalString(entry, "repositoryUri", out string? entryRepository, out bool repositoryShapeValid)
                    || !repositoryShapeValid
                    || !TryReadOptionalString(entry, "revisionId", out string? entryRevision, out bool revisionShapeValid)
                    || !revisionShapeValid)
                {
                    status = SarifEvidenceTrustStatus.UnsupportedShape;
                    detail = "SARIF repositoryUri and revisionId values must be strings when present.";
                    return new ContextReadOutcome(new SarifEvidenceResolvedContext(artifact.LogicalId, repository, revision, null));
                }

                conflict |= !TryMerge(ref repository, entryRepository);
                conflict |= !TryMerge(ref revision, entryRevision);
            }
        }

        SarifEvidenceProducerContext? producer = artifact.ProducerContext;
        string? logicalId = artifact.LogicalId;
        if (producer is not null)
        {
            string? producerLogicalId = NormalizeOptional(producer.LogicalId);
            if (producerLogicalId is not null
                && !string.Equals(producerLogicalId, artifact.LogicalId, StringComparison.Ordinal))
            {
                status = SarifEvidenceTrustStatus.WrongLogicalId;
                detail = "The producer logical identity does not match the artifact logical identity.";
            }

            conflict |= !TryMerge(ref repository, NormalizeOptional(producer.Repository));
            conflict |= !TryMerge(ref revision, NormalizeOptional(producer.Revision));
        }

        string? scope = NormalizeOptional(producer?.Scope);
        if (conflict)
        {
            status = SarifEvidenceTrustStatus.ConflictingContext;
            detail = "SARIF and explicit producer context contain conflicting identity metadata.";
        }

        return new ContextReadOutcome(new SarifEvidenceResolvedContext(logicalId, repository, revision, scope));
    }

    private static bool TryReadOptionalString(
        JsonElement element,
        string propertyName,
        out string? value,
        out bool shapeValid)
    {
        value = null;
        shapeValid = true;
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            shapeValid = false;
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            shapeValid = false;
            return false;
        }

        value = NormalizeOptional(property.GetString());
        return true;
    }

    private static SarifEvidenceTrustStatus? ValidateBindings(
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceAssessmentContext expected,
        SarifEvidenceResolvedContext context,
        out string? detail)
    {
        detail = null;
        if (!string.Equals(context.LogicalId, requirement.Id, StringComparison.Ordinal))
        {
            detail = "The artifact logical identity does not match the configured requirement.";
            return string.IsNullOrWhiteSpace(context.LogicalId)
                ? SarifEvidenceTrustStatus.MissingLogicalId
                : SarifEvidenceTrustStatus.WrongLogicalId;
        }

        SarifEvidenceTrustStatus? status = ValidateBinding(
            requirement.RequireRepository,
            context.Repository,
            expected.Repository,
            SarifEvidenceTrustStatus.MissingRepository,
            SarifEvidenceTrustStatus.WrongRepository,
            "repository",
            out detail);
        if (status is not null)
        {
            return status;
        }

        status = ValidateBinding(
            requirement.RequireRevision,
            context.Revision,
            expected.Revision,
            SarifEvidenceTrustStatus.MissingRevision,
            SarifEvidenceTrustStatus.WrongRevision,
            "revision",
            out detail);
        if (status is not null)
        {
            return status;
        }

        return ValidateBinding(
            requirement.RequireScope,
            context.Scope,
            expected.Scope,
            SarifEvidenceTrustStatus.MissingScope,
            SarifEvidenceTrustStatus.WrongScope,
            "scope",
            out detail);
    }

    private static SarifEvidenceTrustStatus? ValidateBinding(
        bool required,
        string? actual,
        string? expected,
        SarifEvidenceTrustStatus missingStatus,
        SarifEvidenceTrustStatus wrongStatus,
        string label,
        out string? detail)
    {
        detail = null;
        if (!required)
        {
            return null;
        }

        if (actual is null || expected is null)
        {
            detail = $"The required {label} binding is absent from the artifact or assessment context.";
            return missingStatus;
        }

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            detail = $"The SARIF {label} binding does not match the assessment context.";
            return wrongStatus;
        }

        return null;
    }

    private static bool TryMerge(ref string? current, string? incoming)
    {
        if (incoming is null)
        {
            return true;
        }

        if (current is null)
        {
            current = incoming;
            return true;
        }

        return string.Equals(current, incoming, StringComparison.Ordinal);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
