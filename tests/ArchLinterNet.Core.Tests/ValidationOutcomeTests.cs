using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ValidationOutcomeTests
{
    // ClassificationRoles is deliberately an init-only property outside the primary constructor
    // (not a 13th positional parameter), so this 12-argument construction — the shape ValidationOutcome
    // had before ClassificationRoles was introduced — must keep compiling and defaulting to empty.
    private static ValidationOutcome CreatePreExistingShapeOutcome()
    {
        return new ValidationOutcome(
            true,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(),
            "off",
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            "off",
            Array.Empty<PolicyConsistencyDiagnostic>(),
            "off",
            Array.Empty<ArchitectureCoverageSummary>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>());
    }

    [Test]
    public void Construction_WithoutClassificationRoles_DefaultsToEmpty()
    {
        ValidationOutcome outcome = CreatePreExistingShapeOutcome();

        Assert.That(outcome.ClassificationRoles, Is.Empty);
    }

    [Test]
    public void Construction_WithObjectInitializer_SetsClassificationRoles()
    {
        var roles = new[]
        {
            new ArchitectureClassificationRoleFact(
                "MyApp.Order", "DomainLayer", ArchitectureClassificationSource.TypeAttribute, null, new Dictionary<string, object>())
        };

        ValidationOutcome outcome = CreatePreExistingShapeOutcome() with { ClassificationRoles = roles };

        Assert.That(outcome.ClassificationRoles, Is.EqualTo(roles));
    }
}
