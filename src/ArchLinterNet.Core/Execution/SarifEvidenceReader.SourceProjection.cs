using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed partial class SarifEvidenceReader
{
    private static bool TryReadSourceDiagnostics(
        JsonElement run,
        out IReadOnlyList<SarifEvidenceSourceDiagnostic> diagnostics,
        out string? detail,
        CancellationToken cancellationToken)
    {
        diagnostics = Array.Empty<SarifEvidenceSourceDiagnostic>();
        detail = null;

        SarifDriverRuleCatalog driverRules = new();
        if (!TryReadDriverRuleTags(run, driverRules, out detail, cancellationToken))
        {
            return false;
        }

        SarifArtifactCatalog artifacts = new();
        if (!TryReadRunArtifacts(run, artifacts, out detail, cancellationToken))
        {
            return false;
        }

        if (!run.TryGetProperty("results", out JsonElement results))
        {
            return true;
        }

        if (results.ValueKind != JsonValueKind.Array)
        {
            detail = "The SARIF run results member must be an array when present.";
            return false;
        }

        List<SarifEvidenceSourceDiagnostic> parsed = new();
        int index = 0;
        foreach (JsonElement result in results.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadSourceDiagnostic(
                    result,
                    driverRules,
                    artifacts,
                    index,
                    out SarifEvidenceSourceDiagnostic? diagnostic,
                    out detail,
                    cancellationToken))
            {
                diagnostics = Array.Empty<SarifEvidenceSourceDiagnostic>();
                return false;
            }

            parsed.Add(diagnostic!);
            index++;
        }

        diagnostics = Array.AsReadOnly(parsed.ToArray());
        return true;
    }

    private static bool TryReadDriverRuleTags(
        JsonElement run,
        SarifDriverRuleCatalog catalog,
        out string? detail,
        CancellationToken cancellationToken)
    {
        detail = null;
        if (!run.TryGetProperty("tool", out JsonElement tool)
            || tool.ValueKind != JsonValueKind.Object
            || !tool.TryGetProperty("driver", out JsonElement driver)
            || driver.ValueKind != JsonValueKind.Object
            || !driver.TryGetProperty("rules", out JsonElement rules))
        {
            return true;
        }

        if (rules.ValueKind != JsonValueKind.Array)
        {
            detail = "The SARIF tool driver rules member must be an array when present.";
            return false;
        }

        foreach (JsonElement rule in rules.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rule.ValueKind != JsonValueKind.Object)
            {
                detail = "Every SARIF tool driver rule must be an object.";
                return false;
            }

            if (!rule.TryGetProperty("id", out JsonElement id)
                || id.ValueKind != JsonValueKind.String)
            {
                detail = "Every SARIF tool driver rule must declare a string id.";
                return false;
            }

            string ruleId = id.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                detail = "Every SARIF tool driver rule must declare a non-blank id.";
                return false;
            }

            List<string> tags = [];
            if (rule.TryGetProperty("properties", out JsonElement properties))
            {
                if (properties.ValueKind != JsonValueKind.Object)
                {
                    detail = "The SARIF tool driver rule properties member must be an object.";
                    return false;
                }

                if (properties.TryGetProperty("tags", out JsonElement tagValues))
                {
                    if (tagValues.ValueKind != JsonValueKind.Array)
                    {
                        detail = "The SARIF tool driver rule tags member must be an array.";
                        return false;
                    }

                    foreach (JsonElement tag in tagValues.EnumerateArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (tag.ValueKind != JsonValueKind.String)
                        {
                            detail = "Every SARIF tool driver rule tag must be a string.";
                            return false;
                        }

                        tags.Add(tag.GetString() ?? string.Empty);
                    }
                }
            }

            // SARIF allows several descriptors to share an id.  Keep each descriptor in
            // its declared ordinal slot: a result ruleIndex/rule.index is the only way to
            // select the right descriptor (and therefore its tags) in that situation.
            catalog.Add(ruleId, tags);
        }

        return true;
    }

    private static bool TryReadSourceDiagnostic(
        JsonElement result,
        SarifDriverRuleCatalog driverRules,
        SarifArtifactCatalog artifacts,
        int resultIndex,
        out SarifEvidenceSourceDiagnostic? diagnostic,
        out string? detail,
        CancellationToken cancellationToken)
    {
        diagnostic = null;
        detail = null;
        if (result.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} must be an object.";
            return false;
        }

        if (!TryReadResultMessage(result, resultIndex, out string? message, out detail))
        {
            return false;
        }

        if (!TryReadRuleIdentity(result, driverRules, resultIndex, out SarifResolvedRule? resolvedRule, out detail))
        {
            return false;
        }

        SarifEvidenceSourceSeverity severity = SarifEvidenceSourceSeverity.Unspecified;
        if (result.TryGetProperty("level", out JsonElement level))
        {
            if (level.ValueKind != JsonValueKind.String
                || !TryParseSourceSeverity(level.GetString(), out severity))
            {
                detail = $"The SARIF result at index {resultIndex} level must be one of error, warning, note, or none.";
                return false;
            }
        }

        if (!TryReadResultProject(result, resultIndex, out string? project, out detail))
        {
            return false;
        }

        if (!TryReadPrimaryLocation(
                result,
                artifacts,
                resultIndex,
                out SarifEvidenceSourceLocation? primaryLocation,
                out detail,
                cancellationToken))
        {
            return false;
        }

        if (!TryReadFingerprintPairs(
                result,
                "fingerprints",
                isPartial: false,
                resultIndex,
                out IReadOnlyList<SarifEvidenceSourceFingerprint> fingerprints,
                out detail,
                cancellationToken)
            || !TryReadFingerprintPairs(
                result,
                "partialFingerprints",
                isPartial: true,
                resultIndex,
                out IReadOnlyList<SarifEvidenceSourceFingerprint> partialFingerprints,
                out detail,
                cancellationToken))
        {
            return false;
        }

        diagnostic = new SarifEvidenceSourceDiagnostic(
            message,
            resolvedRule?.Id,
            severity,
            primaryLocation,
            project,
            resolvedRule?.Tags ?? Array.Empty<string>(),
            fingerprints,
            partialFingerprints);
        return true;
    }

    private static bool TryReadResultMessage(
        JsonElement result,
        int resultIndex,
        out string? message,
        out string? detail)
    {
        message = null;
        detail = null;
        if (!result.TryGetProperty("message", out JsonElement messageElement))
        {
            detail = $"The SARIF result at index {resultIndex} must contain a message member.";
            return false;
        }

        if (messageElement.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} message member must be an object.";
            return false;
        }

        if (messageElement.TryGetProperty("text", out JsonElement text)
            && text.ValueKind == JsonValueKind.String)
        {
            message = text.GetString();
            return true;
        }

        if (messageElement.TryGetProperty("id", out JsonElement id)
            && id.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(id.GetString()))
        {
            detail =
                $"The SARIF result at index {resultIndex} message.id references are unsupported; " +
                "a resolved message.text is required.";
            return false;
        }

        detail = $"The SARIF result at index {resultIndex} message must contain a string text member.";
        return false;
    }

    private static bool TryReadRuleIdentity(
        JsonElement result,
        SarifDriverRuleCatalog driverRules,
        int resultIndex,
        out SarifResolvedRule? resolvedRule,
        out string? detail)
    {
        resolvedRule = null;
        detail = null;
        if (!TryReadOptionalSourceString(result, "ruleId", resultIndex, out string? directRuleId, out detail))
        {
            return false;
        }

        if (directRuleId is not null && string.IsNullOrWhiteSpace(directRuleId))
        {
            detail = $"The SARIF result at index {resultIndex} ruleId member must be a non-blank string when present.";
            return false;
        }

        int? directRuleIndex = null;
        if (result.TryGetProperty("ruleIndex", out JsonElement directIndex))
        {
            if (!TryReadNonNegativeIndex(directIndex, "ruleIndex", resultIndex, out directRuleIndex, out detail))
            {
                return false;
            }
        }

        string? referencedRuleId = null;
        int? referencedRuleIndex = null;
        if (result.TryGetProperty("rule", out JsonElement ruleReference))
        {
            if (ruleReference.ValueKind != JsonValueKind.Object)
            {
                detail = $"The SARIF result at index {resultIndex} rule member must be an object.";
                return false;
            }

            if (ruleReference.TryGetProperty("id", out JsonElement referenceId))
            {
                if (referenceId.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(referenceId.GetString()))
                {
                    detail = $"The SARIF result at index {resultIndex} rule.id member must be a non-blank string.";
                    return false;
                }

                referencedRuleId = referenceId.GetString();
            }

            if (ruleReference.TryGetProperty("index", out JsonElement referenceIndex))
            {
                if (!TryReadNonNegativeIndex(referenceIndex, "rule.index", resultIndex, out referencedRuleIndex, out detail))
                {
                    return false;
                }
            }

            if (ruleReference.TryGetProperty("guid", out _)
                && !ruleReference.TryGetProperty("id", out _)
                && !ruleReference.TryGetProperty("index", out _))
            {
                detail =
                    $"The SARIF result at index {resultIndex} rule.guid reference cannot be resolved without rule.id or rule.index.";
                return false;
            }

            if (referencedRuleId is null && referencedRuleIndex is null)
            {
                detail =
                    $"The SARIF result at index {resultIndex} rule reference must contain id or index.";
                return false;
            }
        }

        if (directRuleIndex is not null && referencedRuleIndex is not null
            && directRuleIndex != referencedRuleIndex)
        {
            detail = $"The SARIF result at index {resultIndex} contains conflicting rule indexes.";
            return false;
        }

        int? ruleIndex = directRuleIndex ?? referencedRuleIndex;
        SarifDriverRuleDescriptor? indexedDescriptor = null;
        if (ruleIndex is not null)
        {
            if (!driverRules.TryResolve(ruleIndex.Value, out indexedDescriptor))
            {
                detail =
                    $"The SARIF result at index {resultIndex} rule index {ruleIndex.Value} cannot be resolved by the tool driver rules.";
                return false;
            }
        }

        if (directRuleId is not null && referencedRuleId is not null
            && !string.Equals(directRuleId, referencedRuleId, StringComparison.Ordinal))
        {
            detail = $"The SARIF result at index {resultIndex} contains conflicting rule identifiers.";
            return false;
        }

        string? ruleId = directRuleId ?? referencedRuleId ?? indexedDescriptor?.Id;
        if (indexedDescriptor is not null && ruleId is not null
            && !string.Equals(indexedDescriptor.Id, ruleId, StringComparison.Ordinal))
        {
            detail = $"The SARIF result at index {resultIndex} rule reference does not match its rule index.";
            return false;
        }

        if (indexedDescriptor is not null)
        {
            resolvedRule = new SarifResolvedRule(indexedDescriptor.Id, indexedDescriptor.Tags);
            return true;
        }

        if (ruleId is null)
        {
            return true;
        }

        if (driverRules.TryResolveUnique(ruleId, out SarifDriverRuleDescriptor? uniqueDescriptor, out bool isAmbiguous))
        {
            resolvedRule = new SarifResolvedRule(uniqueDescriptor!.Id, uniqueDescriptor.Tags);
            return true;
        }

        if (isAmbiguous)
        {
            detail =
                $"The SARIF result at index {resultIndex} rule id '{ruleId}' resolves to multiple tool driver descriptors; " +
                "a ruleIndex or rule.index is required.";
            return false;
        }

        // A producer can emit a rule without cataloguing it in tool.driver.rules. Its
        // identifier remains a trusted source fact, but there are no descriptor tags.
        resolvedRule = new SarifResolvedRule(ruleId, Array.Empty<string>());
        return true;
    }

    private static bool TryReadNonNegativeIndex(
        JsonElement value,
        string propertyName,
        int resultIndex,
        out int? index,
        out string? detail)
    {
        index = null;
        detail = null;
        if (value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out int parsed)
            || parsed < 0)
        {
            detail =
                $"The SARIF result at index {resultIndex} {propertyName} member must be a non-negative 32-bit integer when present.";
            return false;
        }

        index = parsed;
        return true;
    }

    private static bool TryReadOptionalSourceString(
        JsonElement element,
        string propertyName,
        int resultIndex,
        out string? value,
        out string? detail)
    {
        value = null;
        detail = null;
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            detail = $"The SARIF result at index {resultIndex} {propertyName} member must be a string when present.";
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryParseSourceSeverity(
        string? value,
        out SarifEvidenceSourceSeverity severity)
    {
        severity = value switch
        {
            "error" => SarifEvidenceSourceSeverity.Error,
            "warning" => SarifEvidenceSourceSeverity.Warning,
            "note" => SarifEvidenceSourceSeverity.Note,
            "none" => SarifEvidenceSourceSeverity.None,
            _ => SarifEvidenceSourceSeverity.Unspecified,
        };
        return value is "error" or "warning" or "note" or "none";
    }

    private static bool TryReadResultProject(
        JsonElement result,
        int resultIndex,
        out string? project,
        out string? detail)
    {
        project = null;
        detail = null;
        if (!result.TryGetProperty("properties", out JsonElement properties))
        {
            return true;
        }

        if (properties.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} properties member must be an object when present.";
            return false;
        }

        if (!properties.TryGetProperty("project", out JsonElement projectElement))
        {
            return true;
        }

        if (projectElement.ValueKind != JsonValueKind.String)
        {
            detail = $"The SARIF result at index {resultIndex} properties.project member must be a string when present.";
            return false;
        }

        project = projectElement.GetString();
        return true;
    }

    private sealed class SarifDriverRuleCatalog
    {
        private readonly List<SarifDriverRuleDescriptor> _descriptors = [];
        private readonly Dictionary<string, List<SarifDriverRuleDescriptor>> _descriptorsById = new(StringComparer.Ordinal);

        public void Add(string id, IReadOnlyList<string> tags)
        {
            var descriptor = new SarifDriverRuleDescriptor(id, Array.AsReadOnly(tags.ToArray()));
            _descriptors.Add(descriptor);
            if (!_descriptorsById.TryGetValue(id, out List<SarifDriverRuleDescriptor>? descriptors))
            {
                descriptors = [];
                _descriptorsById.Add(id, descriptors);
            }

            descriptors.Add(descriptor);
        }

        public bool TryResolve(int index, out SarifDriverRuleDescriptor? descriptor)
        {
            if ((uint)index >= (uint)_descriptors.Count)
            {
                descriptor = null;
                return false;
            }

            descriptor = _descriptors[index];
            return true;
        }

        public bool TryResolveUnique(
            string id,
            out SarifDriverRuleDescriptor? descriptor,
            out bool isAmbiguous)
        {
            if (!_descriptorsById.TryGetValue(id, out List<SarifDriverRuleDescriptor>? descriptors))
            {
                descriptor = null;
                isAmbiguous = false;
                return false;
            }

            isAmbiguous = descriptors.Count > 1;
            descriptor = isAmbiguous ? null : descriptors[0];
            return !isAmbiguous;
        }
    }

    private sealed record SarifDriverRuleDescriptor(string Id, IReadOnlyList<string> Tags);

    private sealed record SarifResolvedRule(string Id, IReadOnlyList<string> Tags);
}
