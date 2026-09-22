using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.BuildState;

// The producer's external solution build writes one nonce-bound marker per project only after
// that project's MSBuild Build target succeeds. The marker is not a receipt: it merely proves
// that the authoritative build hand-off happened before verification-only receipt publication.
internal static class BuildStatePreparedBuildProof
{
    private const string ProofSuffix = ".prepared-build-proof";

    internal static bool Exists(
        BuildStatePreflightRequest request,
        ArchitectureDiscoveredProject project,
        string assemblyPath,
        string? proofDirectory,
        string? proofNonce)
    {
        if (string.IsNullOrWhiteSpace(proofDirectory)
            || string.IsNullOrWhiteSpace(proofNonce)
            || !Directory.Exists(proofDirectory))
        {
            return false;
        }

        string projectPath = Path.GetFullPath(
            BuildStatePathResolution.ResolveAbsoluteProjectPath(request.RepositoryRoot, project.Path));
        string expectedAssemblyPath = Path.GetFullPath(assemblyPath);

        foreach (string proofPath in Directory.EnumerateFiles(
            proofDirectory, $"*{ProofSuffix}", SearchOption.TopDirectoryOnly))
        {
            string[] fields;
            try
            {
                fields = File.ReadAllText(proofPath).Trim().Split('|');
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (fields.Length != 7
                || !PathsEqual(fields[0], projectPath)
                || !PathsEqual(fields[1], expectedAssemblyPath)
                || !string.Equals(fields[6], proofNonce, StringComparison.Ordinal))
            {
                continue;
            }

            if (request.RequestedConfiguration is not null
                && !string.Equals(fields[2], request.RequestedConfiguration, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (request.RequestedTargetFramework is not null
                && !string.Equals(fields[3], request.RequestedTargetFramework, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (request.RequestedPlatform is not null
                && !string.Equals(fields[4], request.RequestedPlatform, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (request.RequestedRuntimeIdentifier is not null
                && !string.Equals(fields[5], request.RequestedRuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
