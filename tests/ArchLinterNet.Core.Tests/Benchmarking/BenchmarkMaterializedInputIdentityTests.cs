using System.Security.Cryptography;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Benchmarking;

[TestFixture]
[NonParallelizable]
public sealed class BenchmarkMaterializedInputIdentityTests
{
    [Test]
    public void LoadedAssemblyIdentity_DoesNotDependOnTemporaryFixtureRoot()
    {
        string digest = new('c', 64);
        ArchitectureLoadedAssemblyArtifact firstArtifact = new(
            Path.Combine(Path.GetTempPath(), "fixture-a", "bin", "Debug", "net10.0", "Synthetic.Project000.dll"),
            digest,
            string.Empty,
            2048);
        ArchitectureLoadedAssemblyArtifact secondArtifact = new(
            Path.Combine(Path.GetTempPath(), "fixture-b", "bin", "Debug", "net10.0", "Synthetic.Project000.dll"),
            digest,
            string.Empty,
            2048);

        var first = BenchmarkLoadedAssemblyEvidence.FromLoadedArtifacts([firstArtifact]);
        var second = BenchmarkLoadedAssemblyEvidence.FromLoadedArtifacts([secondArtifact]);
        var changedBytes = BenchmarkLoadedAssemblyEvidence.FromLoadedArtifacts(
        [
            secondArtifact with { AssemblyContentDigest = new string('d', 64) }
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(first.Sha256, Is.EqualTo(second.Sha256));
            Assert.That(first.Artifacts, Is.EqualTo(second.Artifacts));
            Assert.That(changedBytes.Sha256, Is.Not.EqualTo(first.Sha256));
        });
    }

    [TestCase(nameof(BenchmarkCompilationMode.RealMsBuild))]
    [TestCase(nameof(BenchmarkCompilationMode.StagedAssemblies))]
    public void EquivalentWorkloadsHaveStableInputIdentityAcrossIndependentBuildRoots(
        string compilationModeName)
    {
        BenchmarkCompilationMode compilationMode = Enum.Parse<BenchmarkCompilationMode>(compilationModeName);
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            $"synthetic-input-identity-{compilationMode.ToString().ToLowerInvariant()}",
            BenchmarkTopologyShape.Linear,
            new BenchmarkDimensionSet
            {
                ProjectCount = 1,
                TypesPerProject = 2,
                SourceFilesPerProject = 1,
                ReferencesPerProject = 0,
                LayerCount = 2,
                ContractsPerWorkload = 1
            },
            compilationMode);
        using BenchmarkMaterializedFixture first = BenchmarkFixtureMaterializer.Materialize(workload);
        using BenchmarkMaterializedFixture second = BenchmarkFixtureMaterializer.Materialize(workload);

        Assert.That(first.Root, Is.Not.EqualTo(second.Root));
        if (compilationMode == BenchmarkCompilationMode.RealMsBuild)
        {
            first.Build();
            second.Build();
        }

        string firstIdentity = BenchmarkMaterializedInputIdentity.Compute(first.Root);
        string secondIdentity = BenchmarkMaterializedInputIdentity.Compute(second.Root);

        Assert.That(secondIdentity, Is.EqualTo(firstIdentity),
            $"Canonical inputs differed: {string.Join("; ", DescribeDifferingInputs(first.Root, second.Root))}");
    }

    private static string[] DescribeDifferingInputs(string firstRoot, string secondRoot)
    {
        static Dictionary<string, string> HashInputs(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".dll" or ".json" or ".slnx" or ".yml")
                .Where(path =>
                {
                    string normalized = path.Replace('\\', '/');
                    return !normalized.Contains("/obj/", StringComparison.Ordinal)
                           && !normalized.Contains("/bin/", StringComparison.Ordinal)
                           && !normalized.Contains("/analysis-cache/", StringComparison.Ordinal)
                           && !Path.GetFileName(path).StartsWith("profile-", StringComparison.Ordinal)
                           && !Path.GetFileName(path).StartsWith("output-", StringComparison.Ordinal);
                })
                .ToDictionary(
                    path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                    path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                    StringComparer.Ordinal);
        }

        Dictionary<string, string> first = HashInputs(firstRoot);
        Dictionary<string, string> second = HashInputs(secondRoot);
        return first.Keys.Union(second.Keys, StringComparer.Ordinal)
            .Where(path => !first.TryGetValue(path, out string? firstHash)
                           || !second.TryGetValue(path, out string? secondHash)
                           || !string.Equals(firstHash, secondHash, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => DescribeFileDifference(
                Path.Combine(firstRoot, path),
                Path.Combine(secondRoot, path),
                path))
            .ToArray();
    }

    private static string DescribeFileDifference(string firstPath, string secondPath, string relativePath)
    {
        byte[] first = File.ReadAllBytes(firstPath);
        byte[] second = File.ReadAllBytes(secondPath);
        int offset = Enumerable.Range(0, Math.Min(first.Length, second.Length))
            .FirstOrDefault(index => first[index] != second[index], -1);
        if (offset < 0 && first.Length == second.Length) return relativePath;

        string firstBytes = Convert.ToHexString(first.Skip(Math.Max(0, offset - 4)).Take(12).ToArray());
        string secondBytes = Convert.ToHexString(second.Skip(Math.Max(0, offset - 4)).Take(12).ToArray());
        return
            $"{relativePath} (first byte difference {offset}, lengths {first.Length}/{second.Length}, bytes {firstBytes}/{secondBytes})";
    }
}
