using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureSarifFormatter
{
    private static bool HasImportedExternalDiagnosticLocation(SarifEvidenceSourceLocation? location)
    {
        if (location?.Path is { Length: > 0 })
        {
            return true;
        }

        return HasSarifRegionAnchor(location?.Region);
    }

    private static object[] BuildImportedExternalDiagnosticLocations(
        SarifEvidenceSourceLocation location,
        string sourceType,
        string logicalLocationKind)
    {
        if (location.Path is { Length: > 0 } path)
        {
            var physicalLocation = new Dictionary<string, object?>
            {
                [ArtifactLocationKey] = new Dictionary<string, object?> { ["uri"] = path },
            };
            if (TryBuildSarifRegion(location.Region, out Dictionary<string, object?>? region))
            {
                physicalLocation["region"] = region;
            }

            return new object[] { new Dictionary<string, object?> { [PhysicalLocationKey] = physicalLocation } };
        }

        if (TryBuildSarifRegion(location.Region, out Dictionary<string, object?>? annotation))
        {
            // SARIF physical locations require an artifact or address. A selected source result can
            // legitimately provide a region without an artifact URI, so preserve its coordinates as
            // a valid location annotation alongside the deterministic logical location instead.
            return new object[]
            {
                new Dictionary<string, object?>
                {
                    ["logicalLocations"] = BuildLogicalLocationValues(sourceType, logicalLocationKind),
                    ["annotations"] = new object[] { annotation! },
                },
            };
        }

        return BuildLogicalLocations(sourceType, logicalLocationKind);
    }

    private static bool TryBuildSarifRegion(
        SarifEvidenceSourceRegion? source,
        out Dictionary<string, object?>? region)
    {
        region = null;
        if (!HasSarifRegionAnchor(source))
        {
            return false;
        }

        region = new Dictionary<string, object?>();
        if (source!.StartLine is { } startLine)
            region["startLine"] = startLine;
        if (source.StartColumn is { } startColumn)
            region["startColumn"] = startColumn;
        if (source.EndLine is { } endLine)
            region["endLine"] = endLine;
        if (source.EndColumn is { } endColumn)
            region["endColumn"] = endColumn;
        if (source.CharOffset is { } charOffset)
            region["charOffset"] = charOffset;
        if (source.CharLength is { } charLength)
            region["charLength"] = charLength;
        return true;
    }

    private static bool HasSarifRegionAnchor(SarifEvidenceSourceRegion? region) =>
        region?.StartLine is not null || region?.CharOffset is not null;

    private static object[] BuildLogicalLocations(string fullyQualifiedName, string kind)
    {
        return new object[]
        {
            new Dictionary<string, object?>
            {
                ["logicalLocations"] = BuildLogicalLocationValues(fullyQualifiedName, kind),
            },
        };
    }

    private static object[] BuildLogicalLocationValues(string fullyQualifiedName, string kind) =>
    [
        new Dictionary<string, object?>
        {
            ["fullyQualifiedName"] = fullyQualifiedName,
            ["kind"] = kind,
        },
    ];
}
