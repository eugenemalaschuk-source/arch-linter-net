using ArchLinterNet.Core.Model;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CanonicalAnnotationTestingAdapterTests
{
    [Test]
    public void ValidationResult_ExposesInformationalCanonicalDiagnosticsWithoutFailingPassState()
    {
        ArchitectureCanonicalAnnotationDiagnostic diagnostic = ArchitectureCanonicalAnnotationDiagnostic.Create(
            "Example.Order", "MissingCatalogIdentity", "Assembly is missing a catalog identity marker.",
            "ArchLinterNet.Annotations.EntityAttribute");
        var result = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>())
        {
            CanonicalAnnotationDiagnostics = [diagnostic]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.CanonicalAnnotationDiagnostics, Is.EqualTo(new[] { diagnostic }));
            Assert.That(result.Passed, Is.True);
            Assert.DoesNotThrow(result.ShouldPass);
        });
    }
}
