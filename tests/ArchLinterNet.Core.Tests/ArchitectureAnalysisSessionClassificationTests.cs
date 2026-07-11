using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using AttributeRoleExtractionTestFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureAnalysisSessionClassificationTests
{
    private static ArchitectureAnalysisSession CreateSession(ArchitectureClassificationConfiguration classification)
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Classification = classification
        };

        var context = new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(PlainType).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        return new ArchitectureAnalysisSession(context, document, null, false, null);
    }

    [Test]
    public void CheckClassificationFacts_ReturnsConflictsAndFailuresAcrossScannedTypes()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            Attributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.DomainMarkerAttribute",
                    Role = "DomainLayer",
                    Metadata = new Dictionary<string, object> { ["module"] = "property:Module" }
                },
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.SecondMarkerAttribute",
                    Role = "InfrastructureLayer"
                }
            }
        };

        (IReadOnlyList<Model.ArchitectureClassificationConflict> conflicts,
            IReadOnlyList<Model.ArchitectureClassificationMetadataFailure> failures) = CreateSession(classification).CheckClassificationFacts();

        Assert.That(conflicts.Any(c => c.Subject == "AttributeRoleExtractionTestFixtures.TypeWithConflictingEntries"), Is.True);
        Assert.That(
            failures.Any(f => f.Subject == "AttributeRoleExtractionTestFixtures.TypeWithUnsuppliedNamedProperty" && f.MetadataKey == "module"),
            Is.True);
    }

    [Test]
    public void CheckClassificationFacts_AssemblyLevelFailure_IsDeduplicatedAcrossTypes()
    {
        var classification = new ArchitectureClassificationConfiguration
        {
            AssemblyAttributes =
            {
                new ArchitectureAttributeClassificationMapping
                {
                    Attribute = "AttributeRoleExtractionTestFixtures.BoundedContextMarkerAttribute",
                    Role = "ApplicationLayer",
                    Metadata = new Dictionary<string, object> { ["missing"] = "property:DoesNotExist" }
                }
            }
        };

        (_, IReadOnlyList<Model.ArchitectureClassificationMetadataFailure> failures) = CreateSession(classification).CheckClassificationFacts();

        List<Model.ArchitectureClassificationMetadataFailure> assemblyFailures =
            failures.Where(f => f.MetadataKey == "missing").ToList();

        Assert.That(assemblyFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public void CheckClassificationFacts_NoClassificationConfigured_ReturnsEmpty()
    {
        (IReadOnlyList<Model.ArchitectureClassificationConflict> conflicts,
            IReadOnlyList<Model.ArchitectureClassificationMetadataFailure> failures) =
            CreateSession(new ArchitectureClassificationConfiguration()).CheckClassificationFacts();

        Assert.That(conflicts, Is.Empty);
        Assert.That(failures, Is.Empty);
    }
}
