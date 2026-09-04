using ArchLinterNet.Core.Tests.History;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// A checked-in AdoptionAcceptance fixture materialized as its own real git repository, so a test
/// can produce two genuinely different, deterministic base/current commits of the same consumer
/// state instead of two unrelated fixture copies.
/// </summary>
internal sealed class GitVersionedAdoptionFixture : IDisposable
{
    private readonly GitTestRepository _repository;

    private GitVersionedAdoptionFixture(string id, GitTestRepository repository)
    {
        Id = id;
        _repository = repository;
    }

    public string Id { get; }

    public string Root => _repository.Path;

    public string PolicyPath => Path.Combine(Root, "dependencies.arch.yml");

    public static GitVersionedAdoptionFixture Create(string id)
    {
#pragma warning disable CA2000
        // Ownership of the copied fixture directory transfers to the GitTestRepository created at
        // the same path immediately below; AdoptionAcceptanceFixture.Dispose() is intentionally
        // never called so the directory is deleted exactly once, by GitTestRepository.Dispose(),
        // which also clears the read-only attributes git sets on packed/loose objects before
        // deleting them (a plain Directory.Delete cannot).
        AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create(id);
#pragma warning restore CA2000
        GitTestRepository repository = GitTestRepository.CreateAt(fixture.Root);
        return new GitVersionedAdoptionFixture(id, repository);
    }

    public string Commit(string message) => _repository.Commit(message);

    public void Dispose() => _repository.Dispose();
}
