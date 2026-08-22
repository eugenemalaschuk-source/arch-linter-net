using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Enrichment;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class WorktreeHistoryDotNetFactProviderTests
{
    [Test]
    public void Materialize_CleanMatchingCheckoutWithVerifiedArtifact_ReturnsSourceFacts()
    {
        using GitTestRepository repository = CreateEnrichmentRepository(out string head);
        PrepareVerifiedCoreArtifact(repository);

        HistoryDotNetFactMaterialization materialization = new WorktreeHistoryDotNetFactProvider().Materialize(
            repository.Path,
            head,
            "architecture/dependencies.arch.yml");

        HistoryDotNetTypeContext type = materialization.TypesByCanonicalPath[
                "src/Fixture/HistoryIngestionRequest.cs"]
            .Single(candidate => candidate.FullTypeName == "ArchLinterNet.Core.History.HistoryIngestionRequest");
        Assert.Multiple(() =>
        {
            Assert.That(type.ProjectPath, Is.EqualTo("src/Fixture/Fixture.csproj"));
            Assert.That(type.AssemblyName, Is.EqualTo("ArchLinterNet.Core"));
            Assert.That(type.NamespaceName, Is.EqualTo("ArchLinterNet.Core.History"));
        });
    }

    [Test]
    public void Materialize_DirtyMatchingCheckout_ReportsWorktreeDirty()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("README.md", "clean\n");
        string head = repository.Commit("clean");
        repository.Write("dirty.txt", "dirty\n");

        HistoryDotNetEnrichmentUnavailableException exception = Assert.Throws<HistoryDotNetEnrichmentUnavailableException>(() =>
            new WorktreeHistoryDotNetFactProvider().Materialize(
                repository.Path,
                head,
                "architecture/dependencies.arch.yml"))!;

        Assert.That(exception.Reason, Is.EqualTo("worktree_dirty"));
    }

    [Test]
    public void Materialize_MissingRepositoryPolicy_ReportsPolicyLoadFailed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("README.md", "clean\n");
        string head = repository.Commit("clean");

        HistoryDotNetEnrichmentUnavailableException exception = Assert.Throws<HistoryDotNetEnrichmentUnavailableException>(() =>
            new WorktreeHistoryDotNetFactProvider().Materialize(
                repository.Path,
                head,
                "architecture/missing-policy.yml"))!;

        Assert.That(exception.Reason, Is.EqualTo("policy_load_failed"));
    }

    [Test]
    public void Materialize_LinuxCaseVariantSiblingPolicy_IsRejectedAsOutsideRepository()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("README.md", "clean\n");
        string head = repository.Commit("clean");
        string siblingRoot = Path.Combine(
            Path.GetDirectoryName(repository.Path)!,
            Path.GetFileName(repository.Path).ToUpperInvariant());
        string outsidePolicy = Path.Combine(siblingRoot, "policy.yml");

        HistoryDotNetEnrichmentUnavailableException exception = Assert.Throws<HistoryDotNetEnrichmentUnavailableException>(() =>
            new WorktreeHistoryDotNetFactProvider().Materialize(repository.Path, head, outsidePolicy))!;

        Assert.That(exception.Reason, Is.EqualTo("policy_repository_mismatch"));
    }

    private static GitTestRepository CreateEnrichmentRepository(out string head)
    {
        GitTestRepository repository = GitTestRepository.Create();
        repository.Write(".gitignore", "**/bin/\n**/obj/\n");
        repository.Write("src/Fixture/Fixture.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>ArchLinterNet.Core</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        repository.Write("src/Fixture/HistoryIngestionRequest.cs", """
            namespace ArchLinterNet.Core.History;
            internal sealed class HistoryIngestionRequest { }
            """);
        repository.Write("architecture/dependencies.arch.yml", """
            version: 1
            name: Enrichment Fixture
            analysis:
              projects:
                - src/Fixture/Fixture.csproj
              target_assemblies:
                - ArchLinterNet.Core
            """);
        head = repository.Commit("fixture");
        return repository;
    }

    private static void PrepareVerifiedCoreArtifact(GitTestRepository repository)
    {
        const string ProjectPath = "src/Fixture/Fixture.csproj";
        string outputDirectory = Path.Combine(repository.Path, "src", "Fixture", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(outputDirectory);
        string assemblyPath = Path.Combine(outputDirectory, "ArchLinterNet.Core.dll");
        File.Copy(typeof(HistoryIngestionRequest).Assembly.Location, assemblyPath, overwrite: true);
        File.SetLastWriteTimeUtc(assemblyPath, DateTime.UtcNow.AddSeconds(2));
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            ProjectPath,
            "ArchLinterNet.Core",
            Configuration: "Debug",
            TargetFramework: "net10.0",
            BuildStateCanonicalHasher.ComputeBuildInputFingerprint(ProjectPath, repository.Path),
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath)));
    }
}
