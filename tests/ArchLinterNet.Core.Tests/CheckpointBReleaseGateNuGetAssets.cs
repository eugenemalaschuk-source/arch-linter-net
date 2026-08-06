using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

internal static class CheckpointBReleaseGateNuGetAssets
{
    public static bool ContainsPackageFolder(string assetsJson, string expectedPackageFolder)
    {
        using JsonDocument document = JsonDocument.Parse(assetsJson);
        string expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedPackageFolder));
        return document.RootElement.GetProperty("packageFolders")
            .EnumerateObject()
            .Select(folder => Path.TrimEndingDirectorySeparator(folder.Name))
            .Any(folder => string.Equals(folder, expected, StringComparison.OrdinalIgnoreCase));
    }
}
