using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Enrichment;
using ArchLinterNet.Core.History.Reporting;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryDotNetEnricherTests
{
    [Test]
    public void NotRequestedLeavesTheGitOnlyProjectionExplicit()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/Widget.cs", "namespace Example; public class Widget { }\n");
        string first = repository.Commit("first");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, first);
        HistoryDotNetEnrichment enrichment = new HistoryDotNetEnricher(new StubFactProvider())
            .Enrich(result, new HistoryIngestionRequest(repository.Path, first, first), "architecture/dependencies.arch.yml");

        Assert.Multiple(() =>
        {
            Assert.That(enrichment.Status, Is.EqualTo(HistoryDotNetEnrichmentStatus.NotRequested));
            Assert.That(enrichment.Files, Is.Empty);
            Assert.That(result.DotNetEnrichment.Status, Is.EqualTo(HistoryDotNetEnrichmentStatus.NotRequested));
        });
    }

    [Test]
    public void AvailableProjectionMapsFactsOnlyToTheExistingCanonicalCSharpPath()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/Widget.cs", "namespace Example; public class Widget { }\n");
        repository.Write("README.md", "one\n");
        string first = repository.Commit("first");
        repository.Write("src/Widget.cs", "namespace Example; public abstract class Widget { }\n");
        repository.Write("README.md", "two\n");
        string second = repository.Commit("second");
        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, second);
        HistoryDotNetTypeContext type = new(
            "src/Example/Example.csproj", "Example", "Example", "Example.Widget", "Widget",
            ArchitectureTypeKind.Class, isAbstract: true);
        HistoryDotNetEnricher enricher = new(new StubFactProvider(new Dictionary<string, IReadOnlyList<HistoryDotNetTypeContext>>
        {
            ["src/Widget.cs"] = [type]
        }));

        HistoryDotNetEnrichment enrichment = enricher.Enrich(
            result, new HistoryIngestionRequest(repository.Path, first, second, requestDotNetEnrichment: true), "architecture/dependencies.arch.yml");
        string gitOnlyJson = HistoryIngestionJsonWriter.Write(result);
        result.ApplyDotNetEnrichment(enrichment);
        HistoryDotNetFileEnrichment source = enrichment.Files.Single(file => file.CanonicalPath == "src/Widget.cs");

        Assert.Multiple(() =>
        {
            Assert.That(enrichment.Status, Is.EqualTo(HistoryDotNetEnrichmentStatus.Available));
            Assert.That(source.Status, Is.EqualTo(HistoryDotNetFileEnrichmentStatus.Available));
            Assert.That(source.Types, Is.EqualTo(new[] { type }));
            Assert.That(enrichment.Files.Single(file => file.CanonicalPath == "README.md").Status,
                Is.EqualTo(HistoryDotNetFileEnrichmentStatus.NotApplicable));
            Assert.That(HistoryIngestionJsonWriter.Write(result), Is.EqualTo(gitOnlyJson));
        });
    }

    [Test]
    public void UnavailableFactMaterializationPreservesTheCompletedGitResult()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/Widget.cs", "namespace Example; public class Widget { }\n");
        string first = repository.Commit("first");
        repository.Write("src/Widget.cs", "namespace Example; public class Widget { public int Value => 1; }\n");
        string second = repository.Commit("second #9");
        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, second);
        string gitOnlyJson = HistoryIngestionJsonWriter.Write(result);

        HistoryDotNetEnrichment enrichment = new HistoryDotNetEnricher(new ThrowingFactProvider("revision_mismatch"))
            .Enrich(result, new HistoryIngestionRequest(repository.Path, first, second, requestDotNetEnrichment: true), "architecture/dependencies.arch.yml");
        result.ApplyDotNetEnrichment(enrichment);

        Assert.Multiple(() =>
        {
            Assert.That(enrichment.Status, Is.EqualTo(HistoryDotNetEnrichmentStatus.Unavailable));
            Assert.That(enrichment.Reason, Is.EqualTo("revision_mismatch"));
            Assert.That(result.LogicalFiles.Single().CanonicalPath, Is.EqualTo("src/Widget.cs"));
            Assert.That(result.Commits.Single().TaskKeys.Single().IdText, Is.EqualTo("9"));
            Assert.That(HistoryIngestionJsonWriter.Write(result), Is.EqualTo(gitOnlyJson));
            Assert.That(gitOnlyJson, Does.Not.Contain("dotNetEnrichment"));
        });
    }

    [Test]
    public void RevisionMismatchIsReportedBeforePolicyOrBuildFactsAreRead()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/Widget.cs", "namespace Example; public class Widget { }\n");
        string first = repository.Commit("first");
        repository.Write("src/Widget.cs", "namespace Example; public class Widget { public int Value => 1; }\n");
        string second = repository.Commit("second");
        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, first);

        HistoryDotNetEnrichment enrichment = new HistoryDotNetEnricher().Enrich(
            result, new HistoryIngestionRequest(repository.Path, first, first, requestDotNetEnrichment: true), "missing-policy.yml");

        Assert.Multiple(() =>
        {
            Assert.That(enrichment.Status, Is.EqualTo(HistoryDotNetEnrichmentStatus.Unavailable));
            Assert.That(enrichment.Reason, Is.EqualTo("revision_mismatch"));
            Assert.That(result.ResolvedTo, Is.EqualTo(first));
        });
    }

    [Test]
    public void NonDotNetFindingsAreExplicitlyNotApplicable()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("README.md", "one\n");
        string first = repository.Commit("first");
        repository.Write("README.md", "two\n");
        string second = repository.Commit("second");
        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, second);

        HistoryDotNetEnrichment enrichment = new HistoryDotNetEnricher(new StubFactProvider()).Enrich(
            result, new HistoryIngestionRequest(repository.Path, first, second, requestDotNetEnrichment: true), "architecture/dependencies.arch.yml");

        Assert.Multiple(() =>
        {
            Assert.That(enrichment.Status, Is.EqualTo(HistoryDotNetEnrichmentStatus.NotApplicable));
            Assert.That(enrichment.Files.Single().Status, Is.EqualTo(HistoryDotNetFileEnrichmentStatus.NotApplicable));
        });
    }

    [Test]
    public void ProjectionKeepsAmbiguousRenamePathsAsSeparateFiles()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "namespace Example; public class A { }\n");
        string baseCommit = repository.Commit("add A");
        repository.Move("A.cs", "B.cs");
        repository.Commit("A to B");
        repository.Git("checkout", "-q", "-b", "side", baseCommit);
        repository.Move("A.cs", "C.cs");
        repository.Commit("A to C");
        repository.Git("checkout", "-q", "main");
        repository.Git("merge", "-q", "-s", "ours", "-m", "merge", "side");
        string merged = repository.Head();
        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, baseCommit, merged);
        HistoryDotNetEnrichment enrichment = new HistoryDotNetEnricher(new StubFactProvider()).Enrich(
            result, new HistoryIngestionRequest(repository.Path, baseCommit, merged, requestDotNetEnrichment: true), "architecture/dependencies.arch.yml");

        Assert.Multiple(() =>
        {
            Assert.That(result.RenameComponents.Single().StatusText, Is.EqualTo("ambiguous_dag"));
            Assert.That(enrichment.Files.Select(file => file.CanonicalPath), Is.EqualTo(new[] { "A.cs", "B.cs", "C.cs" }));
            Assert.That(enrichment.Files.All(file => file.Status == HistoryDotNetFileEnrichmentStatus.NotApplicable), Is.True);
        });
    }

    private sealed class StubFactProvider(
        IReadOnlyDictionary<string, IReadOnlyList<HistoryDotNetTypeContext>>? facts = null) : IHistoryDotNetFactProvider
    {
        public HistoryDotNetFactMaterialization Materialize(string repositoryPath, string resolvedTo, string policyPath) =>
            new(facts ?? new Dictionary<string, IReadOnlyList<HistoryDotNetTypeContext>>(StringComparer.Ordinal));
    }

    private sealed class ThrowingFactProvider(string reason) : IHistoryDotNetFactProvider
    {
        public HistoryDotNetFactMaterialization Materialize(string repositoryPath, string resolvedTo, string policyPath) =>
            throw new HistoryDotNetEnrichmentUnavailableException(reason);
    }
}
