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

        Dictionary<string, IReadOnlyList<string>> driverRuleTags =
            new(StringComparer.Ordinal);
        if (!TryReadDriverRuleTags(run, driverRuleTags, out detail, cancellationToken))
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
                    driverRuleTags,
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
        Dictionary<string, IReadOnlyList<string>> tagsByRule,
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

            if (tagsByRule.TryGetValue(ruleId, out IReadOnlyList<string>? existing))
            {
                List<string> combined = new(existing.Count + tags.Count);
                combined.AddRange(existing);
                combined.AddRange(tags);
                tagsByRule[ruleId] = Array.AsReadOnly(combined.ToArray());
            }
            else
            {
                tagsByRule.Add(ruleId, Array.AsReadOnly(tags.ToArray()));
            }
        }

        return true;
    }

    private static bool TryReadSourceDiagnostic(
        JsonElement result,
        IReadOnlyDictionary<string, IReadOnlyList<string>> driverRuleTags,
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

        if (!TryReadOptionalSourceString(result, "ruleId", resultIndex, out string? ruleId, out detail))
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

        IReadOnlyList<string> tags = ruleId is not null
            && driverRuleTags.TryGetValue(ruleId, out IReadOnlyList<string>? matchedTags)
            ? matchedTags
            : Array.Empty<string>();

        diagnostic = new SarifEvidenceSourceDiagnostic(
            message,
            ruleId,
            severity,
            primaryLocation,
            project,
            tags,
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
            return true;
        }

        if (messageElement.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} message member must be an object.";
            return false;
        }

        if (!messageElement.TryGetProperty("text", out JsonElement text)
            || text.ValueKind != JsonValueKind.String)
        {
            detail = $"The SARIF result at index {resultIndex} message must contain a string text member.";
            return false;
        }

        message = text.GetString();
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

    private static bool TryReadPrimaryLocation(
        JsonElement result,
        int resultIndex,
        out SarifEvidenceSourceLocation? location,
        out string? detail,
        CancellationToken cancellationToken)
    {
        location = null;
        detail = null;
        if (!result.TryGetProperty("locations", out JsonElement locations))
        {
            return true;
        }

        if (locations.ValueKind != JsonValueKind.Array)
        {
            detail = $"The SARIF result at index {resultIndex} locations member must be an array when present.";
            return false;
        }

        using IEnumerator<JsonElement> enumerator = locations.EnumerateArray().GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        JsonElement first = enumerator.Current;
        if (first.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} primary location must be an object.";
            return false;
        }

        if (!first.TryGetProperty("physicalLocation", out JsonElement physicalLocation))
        {
            return true;
        }

        if (physicalLocation.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} physicalLocation member must be an object.";
            return false;
        }

        string? path = null;
        if (physicalLocation.TryGetProperty("artifactLocation", out JsonElement artifactLocation))
        {
            if (artifactLocation.ValueKind != JsonValueKind.Object)
            {
                detail = $"The SARIF result at index {resultIndex} artifactLocation member must be an object.";
                return false;
            }

            if (artifactLocation.TryGetProperty("uri", out JsonElement uri))
            {
                if (uri.ValueKind != JsonValueKind.String
                    || !TryNormalizeSourcePath(uri.GetString(), out path))
                {
                    detail = $"The SARIF result at index {resultIndex} source location uri must be a repository-relative path.";
                    return false;
                }
            }
        }

        if (!TryReadSourceRegion(
                physicalLocation,
                resultIndex,
                out SarifEvidenceSourceRegion? region,
                out detail,
                cancellationToken))
        {
            return false;
        }

        location = new SarifEvidenceSourceLocation(path, region);
        return true;
    }

    private static bool TryReadSourceRegion(
        JsonElement physicalLocation,
        int resultIndex,
        out SarifEvidenceSourceRegion? region,
        out string? detail,
        CancellationToken cancellationToken)
    {
        region = null;
        detail = null;
        if (!physicalLocation.TryGetProperty("region", out JsonElement regionElement))
        {
            return true;
        }

        if (regionElement.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} region member must be an object.";
            return false;
        }

        if (!TryReadOptionalRegionInt(regionElement, "startLine", resultIndex, out int? startLine, out detail)
            || !TryReadOptionalRegionInt(regionElement, "startColumn", resultIndex, out int? startColumn, out detail)
            || !TryReadOptionalRegionInt(regionElement, "endLine", resultIndex, out int? endLine, out detail)
            || !TryReadOptionalRegionInt(regionElement, "endColumn", resultIndex, out int? endColumn, out detail)
            || !TryReadOptionalRegionInt(regionElement, "charOffset", resultIndex, out int? charOffset, out detail)
            || !TryReadOptionalRegionInt(regionElement, "charLength", resultIndex, out int? charLength, out detail))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        region = new SarifEvidenceSourceRegion(
            startLine,
            startColumn,
            endLine,
            endColumn,
            charOffset,
            charLength);
        return true;
    }

    private static bool TryReadOptionalRegionInt(
        JsonElement region,
        string propertyName,
        int resultIndex,
        out int? value,
        out string? detail)
    {
        value = null;
        detail = null;
        if (!region.TryGetProperty(propertyName, out JsonElement property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out int parsed))
        {
            detail = $"The SARIF result at index {resultIndex} region.{propertyName} member must be a 32-bit integer when present.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryReadFingerprintPairs(
        JsonElement result,
        string propertyName,
        bool isPartial,
        int resultIndex,
        out IReadOnlyList<SarifEvidenceSourceFingerprint> pairs,
        out string? detail,
        CancellationToken cancellationToken)
    {
        pairs = Array.Empty<SarifEvidenceSourceFingerprint>();
        detail = null;
        if (!result.TryGetProperty(propertyName, out JsonElement fingerprints))
        {
            return true;
        }

        if (fingerprints.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} {propertyName} member must be an object when present.";
            return false;
        }

        List<SarifEvidenceSourceFingerprint> parsed = [];
        foreach (JsonProperty pair in fingerprints.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(pair.Name) || pair.Value.ValueKind != JsonValueKind.String)
            {
                detail = $"The SARIF result at index {resultIndex} {propertyName} must contain only non-blank keys with string values.";
                return false;
            }

            parsed.Add(new SarifEvidenceSourceFingerprint(
                pair.Name,
                pair.Value.GetString() ?? string.Empty,
                isPartial));
        }

        pairs = Array.AsReadOnly(parsed.ToArray());
        return true;
    }

    private static bool TryNormalizeSourcePath(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string portable = value.Replace('\\', '/');
        if (portable.StartsWith("/", StringComparison.Ordinal)
            || portable.Contains(':')
            || portable.EndsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = portable.Split('/');
        List<string> retained = new(segments.Length);
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                return false;
            }

            retained.Add(segment);
        }

        if (retained.Count == 0)
        {
            return false;
        }

        normalized = string.Join('/', retained);
        return true;
    }

    private static bool TryReadRunIdentity(
        JsonElement run,
        out SarifRunCandidate candidate,
        out string? shapeError)
    {
        candidate = default;
        shapeError = null;

        if (!run.TryGetProperty("tool", out JsonElement tool))
        {
            return false;
        }

        if (tool.ValueKind != JsonValueKind.Object)
        {
            shapeError = "The SARIF run tool must be an object.";
            return false;
        }

        if (!tool.TryGetProperty("driver", out JsonElement driver))
        {
            return false;
        }

        if (driver.ValueKind != JsonValueKind.Object)
        {
            shapeError = "The SARIF run tool driver must be an object.";
            return false;
        }

        if (!driver.TryGetProperty("name", out JsonElement name))
        {
            return false;
        }

        if (name.ValueKind != JsonValueKind.String)
        {
            shapeError = "The SARIF tool driver name must be a string.";
            return false;
        }

        string? toolVersion = null;
        if (driver.TryGetProperty("version", out JsonElement version))
        {
            if (version.ValueKind != JsonValueKind.String)
            {
                shapeError = "The SARIF tool driver version must be a string.";
                return false;
            }

            toolVersion = version.GetString();
        }

        if (!run.TryGetProperty("automationDetails", out JsonElement automationDetails))
        {
            return false;
        }

        if (automationDetails.ValueKind != JsonValueKind.Object)
        {
            shapeError = "The SARIF run automationDetails must be an object.";
            return false;
        }

        if (!automationDetails.TryGetProperty("id", out JsonElement runId))
        {
            return false;
        }

        if (runId.ValueKind != JsonValueKind.String)
        {
            shapeError = "The SARIF automationDetails id must be a string.";
            return false;
        }

        candidate = new SarifRunCandidate(
            run,
            name.GetString() ?? string.Empty,
            toolVersion,
            runId.GetString() ?? string.Empty);
        return true;
    }

    private static bool HasDuplicateProperties(JsonElement element, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value, cancellationToken))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasDuplicateProperties(item, cancellationToken))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int? ReadResultCount(
        JsonElement run,
        SarifEvidenceLimits limits,
        out SarifEvidenceTrustStatus? status,
        out string? detail)
    {
        status = null;
        detail = null;
        if (!run.TryGetProperty("results", out JsonElement results))
        {
            return 0;
        }

        if (results.ValueKind != JsonValueKind.Array)
        {
            status = SarifEvidenceTrustStatus.UnsupportedShape;
            detail = "The SARIF run results member must be an array when present.";
            return null;
        }

        int count = 0;
        foreach (JsonElement _ in results.EnumerateArray())
        {
            count++;
            if (count > limits.MaxResults)
            {
                status = SarifEvidenceTrustStatus.TooManyResults;
                detail = "The selected SARIF run exceeds the configured result limit.";
                return null;
            }
        }

        return count;
    }

    private static ExecutionState ReadExecutionState(
        JsonElement run,
        out SarifEvidenceTrustStatus? status,
        out string? detail)
    {
        status = null;
        detail = null;
        if (!run.TryGetProperty("invocations", out JsonElement invocations))
        {
            status = SarifEvidenceTrustStatus.IncompleteExecution;
            detail = "The selected SARIF run does not declare invocation success metadata.";
            return default;
        }

        if (invocations.ValueKind != JsonValueKind.Array)
        {
            status = SarifEvidenceTrustStatus.UnsupportedShape;
            detail = "The SARIF run invocations member must be an array.";
            return default;
        }

        int count = 0;
        foreach (JsonElement invocation in invocations.EnumerateArray())
        {
            count++;
            if (invocation.ValueKind != JsonValueKind.Object
                || !invocation.TryGetProperty("executionSuccessful", out JsonElement successful)
                || successful.ValueKind != JsonValueKind.True && successful.ValueKind != JsonValueKind.False)
            {
                status = SarifEvidenceTrustStatus.IncompleteExecution;
                detail = "Every SARIF invocation must explicitly declare a boolean executionSuccessful value.";
                return default;
            }

            if (!successful.GetBoolean())
            {
                status = SarifEvidenceTrustStatus.FailedExecution;
                detail = "The selected SARIF run contains an unsuccessful invocation.";
                return default;
            }
        }

        if (count == 0)
        {
            status = SarifEvidenceTrustStatus.IncompleteExecution;
            detail = "The selected SARIF run must contain at least one successful invocation.";
        }

        return default;
    }

    private readonly record struct SarifRunCandidate(
        JsonElement Run,
        string ToolName,
        string? ToolVersion,
        string RunId);

    private readonly record struct ExecutionState;
}
