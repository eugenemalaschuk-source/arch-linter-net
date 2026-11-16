using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Tests.Benchmarking;

internal sealed record BenchmarkLoadedAssemblyArtifactEvidence
{
    public required string FileName { get; init; }

    public required string Sha256 { get; init; }
}

internal sealed record BenchmarkLoadedAssemblyEvidence
{
    public required string Sha256 { get; init; }

    public required IReadOnlyList<BenchmarkLoadedAssemblyArtifactEvidence> Artifacts { get; init; }

    public static BenchmarkLoadedAssemblyEvidence FromLoadedArtifacts(
        IReadOnlyCollection<ArchitectureLoadedAssemblyArtifact> loadedArtifacts)
    {
        ArgumentNullException.ThrowIfNull(loadedArtifacts);
        BenchmarkLoadedAssemblyArtifactEvidence[] artifacts = loadedArtifacts
            .Select(artifact => new BenchmarkLoadedAssemblyArtifactEvidence
            {
                FileName = Path.GetFileName(artifact.AssemblyPath),
                Sha256 = artifact.AssemblyContentDigest
            })
            .OrderBy(artifact => artifact.FileName, StringComparer.Ordinal)
            .ThenBy(artifact => artifact.Sha256, StringComparer.Ordinal)
            .ToArray();
        BenchmarkLoadedAssemblyEvidence evidence = new()
        {
            Sha256 = ComputeInventorySha256(artifacts),
            Artifacts = artifacts
        };
        evidence.Validate();
        return evidence;
    }

    public void Validate()
    {
        if (Artifacts.Count == 0 || Artifacts.Any(artifact =>
                string.IsNullOrWhiteSpace(artifact.FileName)
                || !string.Equals(Path.GetFileName(artifact.FileName), artifact.FileName, StringComparison.Ordinal)
                || !artifact.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || !IsSha256(artifact.Sha256)))
            throw new InvalidOperationException(
                "Loaded assembly evidence must contain safe DLL names and lowercase SHA-256 digests.");

        BenchmarkLoadedAssemblyArtifactEvidence[] canonicalArtifacts = Artifacts
            .OrderBy(artifact => artifact.FileName, StringComparer.Ordinal)
            .ThenBy(artifact => artifact.Sha256, StringComparer.Ordinal)
            .ToArray();
        if (!string.Equals(Sha256, ComputeInventorySha256(canonicalArtifacts), StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The loaded assembly inventory SHA-256 does not match its artifact identities.");
    }

    private static string ComputeInventorySha256(
        IReadOnlyList<BenchmarkLoadedAssemblyArtifactEvidence> artifacts)
    {
        return BenchmarkIdentity.Sha256(BenchmarkJson.Serialize(artifacts));
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64
               && value.All(Uri.IsHexDigit)
               && string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);
    }
}
