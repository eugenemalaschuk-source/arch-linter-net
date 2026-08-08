namespace ArchLinterNet.Core.Discovery;

internal static class ArchitectureProjectPrimaryArtifactPreserver
{
    public static IReadOnlyList<Snapshot> Capture(string projectAbsolutePath)
    {
        string? projectDirectory = Path.GetDirectoryName(projectAbsolutePath);
        string binDirectory = projectDirectory == null ? string.Empty : Path.Combine(projectDirectory, "bin");
        if (!Directory.Exists(binDirectory))
        {
            return Array.Empty<Snapshot>();
        }

        return Directory.GetFiles(binDirectory, "*", SearchOption.AllDirectories)
            .Where(IsPrimaryArtifact)
            .Select(path => new Snapshot(path, File.ReadAllBytes(path)))
            .ToArray();
    }

    public static void Restore(IReadOnlyList<Snapshot> snapshots)
    {
        foreach (Snapshot snapshot in snapshots)
        {
            if (File.Exists(snapshot.Path) && File.ReadAllBytes(snapshot.Path).AsSpan().SequenceEqual(snapshot.Content))
            {
                continue;
            }

            // Buildalyzer's design-time target can remove outputs. Restoring from a file in the
            // same directory makes the replacement atomic for consumers that load the artifact.
            string temporaryPath = snapshot.Path + $".{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllBytes(temporaryPath, snapshot.Content);
                File.Move(temporaryPath, snapshot.Path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    private static bool IsPrimaryArtifact(string path) =>
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase);

    internal sealed record Snapshot(string Path, byte[] Content);
}
