using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CanonicalAnnotationCandidateComposerTests
{
    [Test]
    public void Merge_WhenCanonicalRoleIsNull_PreservesCombinedConflictsAndMetadataFailures()
    {
        AssertMergedClassificationFacts(canonicalRole: null);
    }

    [Test]
    public void Merge_WhenRolesAreCoherent_PreservesCombinedConflictsAndMetadataFailures()
    {
        AssertMergedClassificationFacts(canonicalRole: "Entity");
    }

    private static void AssertMergedClassificationFacts(string? canonicalRole)
    {
        ArchitectureClassificationConflict configuredConflict = new(
            "ConfiguredSubject",
            ArchitectureClassificationSource.TypeAttribute,
            "Entity",
            "DomainLayer",
            null);
        ArchitectureClassificationConflict canonicalConflict = new(
            "CanonicalSubject",
            ArchitectureClassificationSource.TypeAttribute,
            "Entity",
            "AggregateRoot",
            null);
        ArchitectureClassificationMetadataFailure configuredFailure = new(
            "ConfiguredSubject",
            ArchitectureClassificationSource.TypeAttribute,
            "configured-key",
            "configured failure");
        ArchitectureClassificationMetadataFailure canonicalFailure = new(
            "CanonicalSubject",
            ArchitectureClassificationSource.TypeAttribute,
            "canonical-key",
            "canonical failure");
        ArchitectureAttributeClassificationCandidate configured = new(
            "Entity",
            new Dictionary<string, object>(),
            "Example.CustomEntityAttribute",
            [configuredConflict],
            [configuredFailure]);
        ArchitectureAttributeClassificationCandidate canonical = new(
            canonicalRole,
            new Dictionary<string, object>(),
            canonicalRole is null ? null : "ArchLinterNet.Annotations.EntityAttribute",
            [canonicalConflict],
            [canonicalFailure]);

        ArchitectureAttributeClassificationCandidate result = CanonicalAnnotationCandidateComposer.Merge(
            configured, canonical, "Example.Subject");

        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo("Entity"));
            Assert.That(result.Conflicts, Is.EqualTo(new[] { configuredConflict, canonicalConflict }));
            Assert.That(result.MetadataFailures, Is.EqualTo(new[] { configuredFailure, canonicalFailure }));
        });
    }
}
