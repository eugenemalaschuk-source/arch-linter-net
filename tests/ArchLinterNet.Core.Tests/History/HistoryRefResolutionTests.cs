using ArchLinterNet.Core.History;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryRefResolutionTests
{
    [Test]
    public void AnnotatedTagPeelsToItsCommit()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second #7");
        repository.Git("tag", "-a", "v1.2.3", "-m", "release");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, "v1.2.3");

        Assert.That(result.ResolvedTo, Is.EqualTo(second));
        Assert.That(result.ObjectFormatName, Is.EqualTo("sha1"));
    }

    [Test]
    public void BranchAndTagShorthandCollisionIsAmbiguous()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Git("branch", "release");
        repository.Git("tag", "release");

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, "release");

        Assert.That(diagnostic.KindText, Is.EqualTo("ref_ambiguous"));
    }

    [Test]
    public void RevisionExpressionsAreNotInterpreted()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        repository.Commit("first");
        repository.Write("a.txt", "two\n");
        repository.Commit("second");

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, "HEAD~1", "HEAD");

        Assert.That(diagnostic.KindText, Is.EqualTo("ref_unresolved"));
    }

    [Test]
    public void FullyQualifiedRefsAndHeadResolve()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "two\n");
        string second = repository.Commit("second");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, "refs/heads/main");

        Assert.That(result.ResolvedTo, Is.EqualTo(second));
        Assert.That(HistoryIngestionFixture.Succeed(repository, first, "HEAD").ResolvedTo, Is.EqualTo(second));
    }

    [Test]
    public void UppercaseObjectIdOperandsAreRetainedAsLowercase()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "two\n");
        string second = repository.Commit("second");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first.ToUpperInvariant(), second);

        Assert.That(result.ResolvedFrom, Is.EqualTo(first));
        Assert.That(result.AuthoredFrom, Is.EqualTo(first.ToUpperInvariant()));
    }

    [Test]
    public void ARefResolvingToANonCommitFailsClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        string blobId = repository.Git("rev-parse", "HEAD:a.txt").Trim();

        // Git itself refuses to point refs/heads at a blob, so the fixture uses a ref namespace that
        // permits it — which is exactly the case canonical resolution has to reject on its own.
        repository.Git("update-ref", "refs/fixtures/blobby", blobId);

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, "refs/fixtures/blobby");

        Assert.That(diagnostic.KindText, Is.EqualTo("ref_not_a_commit"));
        Assert.That(diagnostic.ObjectId, Is.EqualTo(blobId));
    }

    [Test]
    public void PackedRefsAreResolved()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "two\n");
        string second = repository.Commit("second");
        repository.Git("tag", "v2.0.0");
        repository.Git("pack-refs", "--all");

        Assert.That(HistoryIngestionFixture.Succeed(repository, first, "v2.0.0").ResolvedTo, Is.EqualTo(second));
    }

    [Test]
    public void PackedObjectsProduceTheSameEvidenceAsLooseObjects()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\ntwo\nthree\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\nthree\nfour\n");
        string second = repository.Commit("second");
        string loose = ArchLinterNet.Core.History.Reporting.HistoryIngestionJsonWriter.Write(
            HistoryIngestionFixture.Succeed(repository, first, second));

        repository.Git("gc", "--aggressive", "--prune=now");
        string packed = ArchLinterNet.Core.History.Reporting.HistoryIngestionJsonWriter.Write(
            HistoryIngestionFixture.Succeed(repository, first, second));

        Assert.That(packed, Is.EqualTo(loose));
    }

    // The tiny blobs above are too small for git to bother deltifying, so `OBJ_OFS_DELTA`
    // reconstruction needs its own fixture: a large enough file, changed enough times, repacked with
    // a delta window. `git verify-pack` confirms real delta objects landed in the pack rather than
    // this test silently degrading into loose-object coverage if a future git version's heuristics
    // change.
    [Test]
    public void OffsetDeltaObjectsReconstructToTheSameEvidenceAsLooseObjects()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        string first = string.Empty;
        string last = string.Empty;
        for (int revision = 0; revision < 6; revision++)
        {
            repository.Write("big.txt", string.Concat(Enumerable.Range(0, 400)
                .Select(line => $"line {line} revision {revision} filler {line * line}\n")));
            last = repository.Commit($"revision {revision}");
            first = revision == 0 ? last : first;
        }

        string loose = ArchLinterNet.Core.History.Reporting.HistoryIngestionJsonWriter.Write(
            HistoryIngestionFixture.Succeed(repository, first, last));

        repository.Git("repack", "-a", "-d", "-f", "-q", "--depth=50", "--window=50");
        string packPath = Directory.GetFiles(Path.Combine(repository.Path, ".git", "objects", "pack"), "*.pack").Single();
        string verify = repository.Git("verify-pack", "-v", packPath);
        Assert.That(verify, Does.Contain("chain length"), "the fixture must actually produce delta objects to exercise OBJ_OFS_DELTA reconstruction");

        string packed = ArchLinterNet.Core.History.Reporting.HistoryIngestionJsonWriter.Write(
            HistoryIngestionFixture.Succeed(repository, first, last));

        Assert.That(packed, Is.EqualTo(loose));
    }
}
