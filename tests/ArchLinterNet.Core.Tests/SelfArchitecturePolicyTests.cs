using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Parity evidence for the canonical read-only gate (`make lint-architecture`, which runs the same
/// policy through the CLI): the ArchLinterNet.Testing adapter reaches the same verdict on the same
/// real repository policy. This is not a second definition of success — see
/// openspec/specs/self-architecture-policy/spec.md.
/// </summary>
[TestFixture]
[Category("E2E")]
// Prepares and verifies the real project graph, then compares three reviewed public API snapshots
// against the live surface. That fits the assembly-wide 15 s per-test limit on an idle machine but
// not dependably on a loaded CI runner, so it takes an explicit, reviewable duration exemption.
[CancelAfter(120_000)]
public sealed class SelfArchitecturePolicyTests
{
    [Test]
    public void RepositoryPolicy_ValidatesOwnInternalBoundaries()
    {
        string repoRoot = SelfPolicyRepository.FindRepositoryRoot();

        ArchitectureValidationResult result = ArchitectureAssertions
            .FromRepositoryRoot(repoRoot)
            .WithEnsureBuilt()
            .ValidateStrict();

        result.ShouldPass();
    }
}
