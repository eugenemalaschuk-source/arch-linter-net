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
        if (!HasUsableProofDirectory(proofDirectory, proofNonce))
        {
            return false;
        }

        string projectPath = Path.GetFullPath(
            BuildStatePathResolution.ResolveAbsoluteProjectPath(request.RepositoryRoot, project.Path));
        string expectedAssemblyPath = Path.GetFullPath(assemblyPath);

        return Directory.EnumerateFiles(
                proofDirectory!, $"*{ProofSuffix}", SearchOption.TopDirectoryOnly)
            .Any(proofPath => MatchesProof(proofPath, request, projectPath, expectedAssemblyPath, proofNonce!));
    }

    private static bool HasUsableProofDirectory(string? proofDirectory, string? proofNonce) =>
        !string.IsNullOrWhiteSpace(proofDirectory)
        && !string.IsNullOrWhiteSpace(proofNonce)
        && Directory.Exists(proofDirectory);

    private static bool MatchesProof(
        string proofPath,
        BuildStatePreflightRequest request,
        string projectPath,
        string expectedAssemblyPath,
        string proofNonce)
    {
        if (!TryReadFields(proofPath, out string[] fields))
        {
            return false;
        }

        return fields.Length == 7
            && PathsEqual(fields[0], projectPath)
            && PathsEqual(fields[1], expectedAssemblyPath)
            && string.Equals(fields[6], proofNonce, StringComparison.Ordinal)
            && MatchesRequestedBuildProperties(fields, request);
    }

    private static bool TryReadFields(string proofPath, out string[] fields)
    {
        try
        {
            fields = File.ReadAllText(proofPath).Trim().Split('|');
            return true;
        }
        catch (IOException)
        {
            fields = [];
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            fields = [];
            return false;
        }
    }

    private static bool MatchesRequestedBuildProperties(string[] fields, BuildStatePreflightRequest request) =>
        MatchesRequestedValue(fields[2], request.RequestedConfiguration)
        && MatchesRequestedValue(fields[3], request.RequestedTargetFramework)
        && MatchesRequestedValue(fields[4], request.RequestedPlatform)
        && MatchesRequestedValue(fields[5], request.RequestedRuntimeIdentifier);

    private static bool MatchesRequestedValue(string actual, string? requested) =>
        requested is null || string.Equals(actual, requested, StringComparison.OrdinalIgnoreCase);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
