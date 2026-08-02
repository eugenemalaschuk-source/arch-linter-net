using ArchLinterNet.Core.BuildState;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class EvaluatedBuildInputManifestTests
{
    [Test]
    public void Collect_LinkedRepositorySourceChange_ChangesPortableDigest()
    {
        using ManifestFixture fixture = ManifestFixture.Create("<ItemGroup><Compile Include=\"../Shared/Linked.cs\" Link=\"Linked.cs\" /></ItemGroup>");
        string linked = Path.Combine(fixture.Root, "src", "Shared", "Linked.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(linked)!);
        File.WriteAllText(linked, "namespace Shared; public class Linked {} ");

        EvaluatedBuildInputManifestV1 first = fixture.Collect();
        File.WriteAllText(linked, "namespace Shared; public class Linked { public int Value; } ");
        EvaluatedBuildInputManifestV1 second = fixture.Collect();

        Assert.That(first.Eligibility, Is.EqualTo(CacheEligibility.VerifiedCacheEligible));
        Assert.That(second.Digest, Is.Not.EqualTo(first.Digest));
    }

    [Test]
    public void Collect_CustomImportedTargetChange_ChangesDigest()
    {
        using ManifestFixture fixture = ManifestFixture.Create("<Import Project=\"custom.targets\" />");
        string target = Path.Combine(fixture.ProjectDirectory, "custom.targets");
        File.WriteAllText(target, "<Project />");

        EvaluatedBuildInputManifestV1 first = fixture.Collect();
        File.WriteAllText(target, "<Project><PropertyGroup><X>1</X></PropertyGroup></Project>");
        EvaluatedBuildInputManifestV1 second = fixture.Collect();

        Assert.That(first.Eligibility, Is.EqualTo(CacheEligibility.VerifiedCacheEligible));
        Assert.That(second.Digest, Is.Not.EqualTo(first.Digest));
    }

    [Test]
    public void Collect_ContextAndReferenceIdentityChanges_DoNotCollide()
    {
        using ManifestFixture fixture = ManifestFixture.Create("<ItemGroup><PackageReference Include=\"Example\" Version=\"1.0.0\" /></ItemGroup>");

        EvaluatedBuildInputManifestV1 debug = fixture.Collect(configuration: "Debug", targetFramework: "net10.0", platform: "AnyCPU", runtimeIdentifier: "linux-x64");
        EvaluatedBuildInputManifestV1 release = fixture.Collect(configuration: "Release", targetFramework: "net10.0", platform: "AnyCPU", runtimeIdentifier: "linux-x64");
        File.WriteAllText(fixture.ProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"Example\" Version=\"2.0.0\" /></ItemGroup></Project>");
        EvaluatedBuildInputManifestV1 changedReference = fixture.Collect(configuration: "Debug", targetFramework: "net10.0", platform: "AnyCPU", runtimeIdentifier: "linux-x64");

        Assert.That(release.Digest, Is.Not.EqualTo(debug.Digest));
        Assert.That(changedReference.Digest, Is.Not.EqualTo(debug.Digest));
    }

    [Test]
    public void Collect_UnsupportedDynamicInput_IsCacheIneligible()
    {
        using ManifestFixture fixture = ManifestFixture.Create("<ItemGroup><AdditionalFiles Include=\"config/*.json\" /></ItemGroup>");

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.Eligibility, Is.EqualTo(CacheEligibility.CacheIneligible));
        Assert.That(result.IneligibilityReasons, Does.Contain("uninspectable-additionalfiles-input"));
    }

    [Test]
    public void Collect_EquivalentCheckoutRoots_HasEquivalentDigest()
    {
        using ManifestFixture first = ManifestFixture.Create(string.Empty);
        using ManifestFixture second = ManifestFixture.Create(string.Empty);

        Assert.That(second.Collect().Digest, Is.EqualTo(first.Collect().Digest));
    }

    private sealed class ManifestFixture : IDisposable
    {
        private ManifestFixture(string root, string projectDirectory, string projectPath)
        {
            Root = root;
            ProjectDirectory = projectDirectory;
            ProjectPath = projectPath;
        }

        public string Root { get; }
        public string ProjectDirectory { get; }
        public string ProjectPath { get; }

        public static ManifestFixture Create(string projectContent)
        {
            string root = Path.Combine(Path.GetTempPath(), $"arch-linter-manifest-{Guid.NewGuid():N}");
            string directory = Path.Combine(root, "src", "Fixture");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Fixture.csproj");
            File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>" + projectContent + "</Project>");
            File.WriteAllText(Path.Combine(directory, "Class1.cs"), "namespace Fixture; public class C {} ");
            return new ManifestFixture(root, directory, path);
        }

        public EvaluatedBuildInputManifestV1 Collect(string? configuration = null, string? targetFramework = null,
            string? platform = null, string? runtimeIdentifier = null) =>
            EvaluatedBuildInputManifestCollector.Collect("src/Fixture/Fixture.csproj", Root, configuration, targetFramework, platform, runtimeIdentifier);

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
