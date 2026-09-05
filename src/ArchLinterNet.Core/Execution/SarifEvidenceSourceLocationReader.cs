using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

/// <summary>Projects SARIF artifact locations, regions, and fingerprints into source facts.</summary>
internal sealed class SarifEvidenceSourceLocationReader
{
    internal static bool TryReadPrimaryLocation(
        JsonElement result,
        SarifArtifactCatalog artifacts,
        int resultIndex,
        out SarifEvidenceSourceLocation? location,
        out string? detail,
        CancellationToken cancellationToken)
    {
        location = null;
        detail = null;
        if (!TryReadFirstLocation(
                result,
                resultIndex,
                out JsonElement first,
                out detail,
                cancellationToken)
            || first.ValueKind == JsonValueKind.Undefined
            || !first.TryGetProperty("physicalLocation", out JsonElement physicalLocation))
        {
            return detail is null;
        }

        if (!TryReadPhysicalLocation(
                physicalLocation,
                artifacts,
                resultIndex,
                out string? path,
                out SarifEvidenceSourceRegion? region,
                out detail,
                cancellationToken))
        {
            return false;
        }

        // A location must carry a normalized-schema anchor. A path always anchors it; without a
        // path only a start line or character offset does. Empty and column-only SARIF regions
        // contain no usable primary-location fact, so collapse them instead of serializing an
        // all-null/anchorless normalized location.
        location = path is null && !HasNormalizedLocationAnchor(region)
            ? null
            : new SarifEvidenceSourceLocation(path, region);
        return true;
    }

    private static bool TryReadFirstLocation(
        JsonElement result,
        int resultIndex,
        out JsonElement first,
        out string? detail,
        CancellationToken cancellationToken)
    {
        first = default;
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

        JsonElement.ArrayEnumerator enumerator = locations.EnumerateArray();
        if (!enumerator.MoveNext())
        {
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        first = enumerator.Current;
        if (first.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        detail = $"The SARIF result at index {resultIndex} primary location must be an object.";
        return false;
    }

    private static bool TryReadPhysicalLocation(
        JsonElement physicalLocation,
        SarifArtifactCatalog artifacts,
        int resultIndex,
        out string? path,
        out SarifEvidenceSourceRegion? region,
        out string? detail,
        CancellationToken cancellationToken)
    {
        path = null;
        region = null;
        detail = null;
        if (physicalLocation.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} physicalLocation member must be an object.";
            return false;
        }

        if (!TryReadArtifactPath(physicalLocation, artifacts, resultIndex, out path, out detail)
            || !TryReadSourceRegion(physicalLocation, resultIndex, out region, out detail, cancellationToken))
        {
            return false;
        }

        return true;
    }

    private static bool TryReadArtifactPath(
        JsonElement physicalLocation,
        SarifArtifactCatalog artifacts,
        int resultIndex,
        out string? path,
        out string? detail)
    {
        path = null;
        detail = null;
        if (!physicalLocation.TryGetProperty("artifactLocation", out JsonElement artifactLocation))
        {
            return true;
        }

        if (artifactLocation.ValueKind != JsonValueKind.Object)
        {
            detail = $"The SARIF result at index {resultIndex} artifactLocation member must be an object.";
            return false;
        }

        string? directPath = null;
        if (artifactLocation.TryGetProperty("uri", out JsonElement uri)
            && (uri.ValueKind != JsonValueKind.String
                || !TryNormalizeSourcePath(uri.GetString(), out directPath)))
        {
            detail = $"The SARIF result at index {resultIndex} source location uri must be a repository-relative path.";
            return false;
        }

        int? artifactIndex = null;
        if (artifactLocation.TryGetProperty("index", out JsonElement index)
            && !TryReadNonNegativeIndex(index, "artifactLocation.index", resultIndex, out artifactIndex, out detail))
        {
            return false;
        }

        if (artifactIndex is null)
        {
            path = directPath;
            return true;
        }

        if (!artifacts.TryResolve(artifactIndex.Value, out path))
        {
            detail =
                $"The SARIF result at index {resultIndex} artifactLocation.index {artifactIndex.Value} cannot be resolved by run.artifacts.";
            return false;
        }

        if (directPath is not null && !string.Equals(path, directPath, StringComparison.Ordinal))
        {
            detail = $"The SARIF result at index {resultIndex} artifactLocation uri and index resolve to different paths.";
            return false;
        }

        return true;
    }

    internal static bool TryReadRunArtifacts(
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
            if (!TryReadRunArtifact(artifact, out string? path, out detail))
            {
                return false;
            }

            artifacts.Add(path);
        }

        return true;
    }

    private static bool TryReadRunArtifact(
        JsonElement artifact,
        out string? path,
        out string? detail)
    {
        path = null;
        detail = null;
        if (artifact.ValueKind != JsonValueKind.Object)
        {
            detail = "Every SARIF run artifact must be an object.";
            return false;
        }

        if (artifact.TryGetProperty("location", out JsonElement location))
        {
            return TryReadArtifactLocation(location, out path, out detail);
        }

        return !artifact.TryGetProperty("artifactLocation", out JsonElement artifactLocation)
            || TryReadArtifactLocation(artifactLocation, out path, out detail);
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

    private static bool TryNormalizeSourcePath(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string portable = value.Replace('\\', '/');
        if (portable.StartsWith('/')
            || portable.Contains(':')
            || portable.EndsWith('/'))
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

    private static bool HasNormalizedLocationAnchor(SarifEvidenceSourceRegion? region) =>
        region?.StartLine is not null || region?.CharOffset is not null;
}

internal sealed class SarifArtifactCatalog
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
