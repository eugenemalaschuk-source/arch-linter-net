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

        Assert.That(first.Eligibility, Is.EqualTo(CacheEligibility.CacheIneligible));
        Assert.That(first.IneligibilityReasons, Does.Contain("evaluated-msbuild-evidence-incomplete"));
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

        Assert.That(first.Eligibility, Is.EqualTo(CacheEligibility.CacheIneligible));
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
    public void Collect_MalformedProjectXml_IsCacheIneligible()
    {
        using ManifestFixture fixture = ManifestFixture.Create(string.Empty);
        File.WriteAllText(fixture.ProjectPath, "<Project>");

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.Eligibility, Is.EqualTo(CacheEligibility.CacheIneligible));
        Assert.That(result.IneligibilityReasons, Does.Contain("project-xml-uninspectable"));
    }

    [Test]
    public void Collect_ExplicitMissingCompileInput_IsCacheIneligible()
    {
        using ManifestFixture fixture = ManifestFixture.Create("<ItemGroup><Compile Include=\"Missing.cs\" /></ItemGroup>");

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.Eligibility, Is.EqualTo(CacheEligibility.CacheIneligible));
        Assert.That(result.IneligibilityReasons, Does.Contain("missing-compile-input"));
    }

    [Test]
    public void Collect_LocalAnalyzerInput_IsHashedButCacheIneligible()
    {
        using ManifestFixture fixture = ManifestFixture.Create("<ItemGroup><Analyzer Include=\"tools/Fixture.Analyzers.dll\" /></ItemGroup>");
        string analyzer = Path.Combine(fixture.ProjectDirectory, "tools", "Fixture.Analyzers.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(analyzer)!);
        File.WriteAllText(analyzer, "not-an-assembly");

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.Inputs, Does.Contain("file:src/Fixture/tools/Fixture.Analyzers.dll"));
        Assert.That(result.IneligibilityReasons, Does.Contain("analyzer-or-generator-identity-unverified"));
    }

    [Test]
    public void Collect_InputExceedingByteBudget_IsRejectedBeforeHashing()
    {
        using ManifestFixture fixture = ManifestFixture.Create(string.Empty);
        string largeSource = Path.Combine(fixture.ProjectDirectory, "Large.cs");
        using (FileStream stream = File.Create(largeSource))
        {
            stream.SetLength(64L * 1024 * 1024);
        }

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.Eligibility, Is.EqualTo(CacheEligibility.CacheIneligible));
        Assert.That(result.IneligibilityReasons, Does.Contain("input-byte-limit-exceeded"));
        Assert.That(result.Inputs, Does.Not.Contain("file:src/Fixture/Large.cs"));
    }

    [Test]
    public void Collect_ProjectExactlyFillingByteBudget_StopsBeforeSubsequentCollectionPhases()
    {
        using ManifestFixture fixture = ManifestFixture.Create(string.Empty);
        using (FileStream stream = File.Open(fixture.ProjectPath, FileMode.Open, FileAccess.Write))
        {
            stream.SetLength(64L * 1024 * 1024);
        }
        File.WriteAllText(Path.Combine(fixture.ProjectDirectory, "Late.cs"), "namespace Fixture; public class Late {} ");

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.IneligibilityReasons, Does.Contain("input-byte-budget-exhausted"));
        Assert.That(result.Inputs, Does.Not.Contain("file:src/Fixture/Late.cs"));
        Assert.That(result.Inputs, Does.Not.Contain("sdk"));
        Assert.That(result.Inputs, Does.Contain("context:configuration"));
    }

    [Test]
    public void Collect_ProjectExceedingByteBudget_StopsBeforeSubsequentCollectionPhases()
    {
        using ManifestFixture fixture = ManifestFixture.Create(string.Empty);
        using (FileStream stream = File.Open(fixture.ProjectPath, FileMode.Open, FileAccess.Write))
        {
            stream.SetLength(64L * 1024 * 1024 + 1);
        }
        File.WriteAllText(Path.Combine(fixture.ProjectDirectory, "Late.cs"), "namespace Fixture; public class Late {} ");

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.IneligibilityReasons, Does.Contain("input-byte-limit-exceeded"));
        Assert.That(result.Inputs, Does.Not.Contain("file:src/Fixture/Late.cs"));
        Assert.That(result.Inputs, Does.Not.Contain("sdk"));
        Assert.That(result.Inputs, Does.Contain("context:runtimeIdentifier"));
    }

    [Test]
    public void Collect_ExhaustedBudget_PreservesDistinctConfigurationAndRuntimeContexts()
    {
        using ManifestFixture fixture = ManifestFixture.Create(string.Empty);
        using (FileStream stream = File.Open(fixture.ProjectPath, FileMode.Open, FileAccess.Write))
        {
            stream.SetLength(64L * 1024 * 1024);
        }

        EvaluatedBuildInputManifestV1 debug = fixture.Collect(configuration: "Debug", platform: "AnyCPU", runtimeIdentifier: "linux-x64");
        EvaluatedBuildInputManifestV1 release = fixture.Collect(configuration: "Release", platform: "x64", runtimeIdentifier: "linux-x64");
        EvaluatedBuildInputManifestV1 differentRuntime = fixture.Collect(configuration: "Debug", platform: "AnyCPU", runtimeIdentifier: "win-x64");

        Assert.That(release.Digest, Is.Not.EqualTo(debug.Digest));
        Assert.That(differentRuntime.Digest, Is.Not.EqualTo(debug.Digest));
    }

    [Test]
    public void Collect_ReferenceValuesExceedingInputBudget_AreRejected()
    {
        string references = string.Join(string.Empty, Enumerable.Range(0, 10_001)
            .Select(index => $"<PackageReference Include=\"Package{index}\" Version=\"1.0.0\" />"));
        using ManifestFixture fixture = ManifestFixture.Create($"<ItemGroup>{references}</ItemGroup>");

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.IneligibilityReasons, Does.Contain("input-limit-exceeded"));
        Assert.That(result.Inputs.Count, Is.LessThanOrEqualTo(10_004));
    }

    [Test]
    public void Collect_MissingNestedCompileInput_ReportsMissingInsteadOfSymlink()
    {
        using ManifestFixture fixture = ManifestFixture.Create("<ItemGroup><Compile Include=\"missing/Thing.cs\" /></ItemGroup>");

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.IneligibilityReasons, Does.Contain("missing-compile-input"));
        Assert.That(result.IneligibilityReasons, Does.Not.Contain("symlink-input-unverified"));
    }

    [Test]
    [Platform(Exclude = "Win", Reason = "Creating symbolic links requires a Windows developer privilege.")]
    public void Collect_ExplicitSymlinkInput_IsRejected()
    {
        using ManifestFixture fixture = ManifestFixture.Create("<ItemGroup><Compile Include=\"Linked.cs\" /></ItemGroup>");
        string target = Path.Combine(fixture.ProjectDirectory, "Real.cs");
        File.WriteAllText(target, "namespace Fixture; public class Real {} ");
        File.CreateSymbolicLink(Path.Combine(fixture.ProjectDirectory, "Linked.cs"), target);

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.Eligibility, Is.EqualTo(CacheEligibility.CacheIneligible));
        Assert.That(result.IneligibilityReasons, Does.Contain("symlink-input-unverified"));
        Assert.That(result.Inputs, Does.Not.Contain("file:src/Fixture/Linked.cs"));
    }

    [Test]
    [Platform(Exclude = "Win", Reason = "Creating symbolic links requires a Windows developer privilege.")]
    public void Collect_InputBelowSymlinkDirectory_IsRejectedBeforeHashing()
    {
        using ManifestFixture fixture = ManifestFixture.Create("<ItemGroup><Compile Include=\"linked/Linked.cs\" /></ItemGroup>");
        string targetDirectory = Path.Combine(fixture.ProjectDirectory, "target");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, "Linked.cs"), "namespace Fixture; public class Linked {} ");
        Directory.CreateSymbolicLink(Path.Combine(fixture.ProjectDirectory, "linked"), targetDirectory);

        EvaluatedBuildInputManifestV1 result = fixture.Collect();

        Assert.That(result.IneligibilityReasons, Does.Contain("symlink-input-unverified"));
        Assert.That(result.Inputs, Does.Not.Contain("file:src/Fixture/linked/Linked.cs"));
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
