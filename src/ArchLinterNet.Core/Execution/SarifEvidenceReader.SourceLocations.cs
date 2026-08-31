using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed partial class SarifEvidenceReader
{
    private static bool TryReadPrimaryLocation(
        JsonElement result,
        SarifArtifactCatalog artifacts,
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

            string? directPath = null;
            if (artifactLocation.TryGetProperty("uri", out JsonElement uri))
            {
                if (uri.ValueKind != JsonValueKind.String
                    || !TryNormalizeSourcePath(uri.GetString(), out directPath))
                {
                    detail = $"The SARIF result at index {resultIndex} source location uri must be a repository-relative path.";
                    return false;
                }
            }

            int? artifactIndex = null;
            if (artifactLocation.TryGetProperty("index", out JsonElement index))
            {
                if (!TryReadNonNegativeIndex(index, "artifactLocation.index", resultIndex, out artifactIndex, out detail))
                {
                    return false;
                }
            }

            if (artifactIndex is not null)
            {
                if (!artifacts.TryResolve(artifactIndex.Value, out path))
                {
                    detail =
                        $"The SARIF result at index {resultIndex} artifactLocation.index {artifactIndex.Value} cannot be resolved by run.artifacts.";
                    return false;
                }

                if (directPath is not null
                    && !string.Equals(path, directPath, StringComparison.Ordinal))
                {
                    detail =
                        $"The SARIF result at index {resultIndex} artifactLocation uri and index resolve to different paths.";
                    return false;
                }
            }
            else
            {
                path = directPath;
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

        // An empty physicalLocation carries no usable source fact. Preserve a real path or region,
        // but collapse `{ "physicalLocation": {} }` to no location so downstream normalized
        // findings never serialize an all-null location that violates their own schema.
        location = path is null && region is null
            ? null
            : new SarifEvidenceSourceLocation(path, region);
        return true;
    }

    private static bool TryReadRunArtifacts(
        JsonElement run,
        SarifArtifactCatalog artifacts,
        out string? detail,
        CancellationToken cancellationToken)
    {
        detail = null;
        if (!run.TryGetProperty("artifacts", out JsonElement artifactsElement))
        {
            return true;
        }

        if (artifactsElement.ValueKind != JsonValueKind.Array)
        {
            detail = "The SARIF run artifacts member must be an array when present.";
            return false;
        }

        foreach (JsonElement artifact in artifactsElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (artifact.ValueKind != JsonValueKind.Object)
            {
                detail = "Every SARIF run artifact must be an object.";
                return false;
            }

            string? path = null;
            if (artifact.TryGetProperty("location", out JsonElement location))
            {
                if (!TryReadArtifactLocation(location, out path, out detail))
                {
                    return false;
                }
            }
            else if (artifact.TryGetProperty("artifactLocation", out JsonElement artifactLocation))
            {
                if (!TryReadArtifactLocation(artifactLocation, out path, out detail))
                {
                    return false;
                }
            }

            artifacts.Add(path);
        }

        return true;
    }

    private static bool TryReadArtifactLocation(
        JsonElement location,
        out string? path,
        out string? detail)
    {
        path = null;
        detail = null;
        if (location.ValueKind != JsonValueKind.Object)
        {
            detail = "A SARIF artifact location must be an object.";
            return false;
        }

        if (!location.TryGetProperty("uri", out JsonElement uri))
        {
            return true;
        }

        if (uri.ValueKind != JsonValueKind.String
            || !TryNormalizeSourcePath(uri.GetString(), out path))
        {
            detail = "A SARIF artifact location uri must be a repository-relative path.";
            return false;
        }

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

        if (!TryReadOptionalRegionInt(regionElement, "startLine", 1, resultIndex, out int? startLine, out detail)
            || !TryReadOptionalRegionInt(regionElement, "startColumn", 1, resultIndex, out int? startColumn, out detail)
            || !TryReadOptionalRegionInt(regionElement, "endLine", 1, resultIndex, out int? endLine, out detail)
            || !TryReadOptionalRegionInt(regionElement, "endColumn", 1, resultIndex, out int? endColumn, out detail)
            || !TryReadOptionalRegionInt(regionElement, "charOffset", 0, resultIndex, out int? charOffset, out detail)
            || !TryReadOptionalRegionInt(regionElement, "charLength", 0, resultIndex, out int? charLength, out detail))
        {
            return false;
        }

        if (endLine is not null && startLine is not null && endLine < startLine)
        {
            detail = $"The SARIF result at index {resultIndex} region.endLine must not precede region.startLine.";
            return false;
        }

        if (endLine == startLine
            && endColumn is not null
            && startColumn is not null
            && endColumn < startColumn)
        {
            detail = $"The SARIF result at index {resultIndex} region.endColumn must not precede region.startColumn on the same line.";
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
        int minimum,
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

        if (parsed < minimum)
        {
            detail = $"The SARIF result at index {resultIndex} region.{propertyName} member must be at least {minimum} when present.";
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
    private sealed class SarifArtifactCatalog
    {
        private readonly List<string?> _paths = [];

        public void Add(string? path) => _paths.Add(path);

        public bool TryResolve(int index, out string? path)
        {
            if ((uint)index >= (uint)_paths.Count || _paths[index] is null)
            {
                path = null;
                return false;
            }

            path = _paths[index];
            return true;
        }
    }
}
