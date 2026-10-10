using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Validators;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CanonicalAnnotationCatalogTests
{
    [Test]
    public void RuntimeCatalog_MatchesEveryManifestRoleIdentityAndScope()
    {
        string root = SelfPolicyRepository.FindRepositoryRoot();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "architecture", "semantic-annotations.v1.json")));
        JsonElement rootElement = manifest.RootElement;
        CanonicalAnnotationCatalog catalog = CanonicalAnnotationCatalog.Current;

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Roles, Has.Count.EqualTo(49));
            Assert.That(catalog.Generation, Is.EqualTo(rootElement.GetProperty("catalogGeneration").GetInt32()));
            Assert.That(catalog.ReservedNamespace, Is.EqualTo(rootElement.GetProperty("reservedNamespace").GetString()));
            Assert.That(catalog.CatalogIdentityMarker,
                Is.EqualTo(rootElement.GetProperty("catalogIdentityMarker").GetProperty("fqn").GetString()));
            Assert.That(catalog.SupportedPackageRange,
                Is.EqualTo(rootElement.GetProperty("package").GetProperty("compatiblePackageRangeForGeneration1").GetString()));
        });

        var expected = rootElement.GetProperty("roles").EnumerateArray()
            .Select(role => new
            {
                Name = role.GetProperty("role").GetString(),
                Fqn = role.GetProperty("fqn").GetString(),
                Scopes = role.GetProperty("scopes").EnumerateArray().Select(value => value.GetString()).ToArray()
            })
            .OrderBy(role => role.Fqn, StringComparer.Ordinal)
            .ToArray();
        var actual = catalog.Roles
            .OrderBy(role => role.AttributeFullName, StringComparer.Ordinal)
            .ToArray();

        Assert.That(actual.Select(role => role.Name), Is.EqualTo(expected.Select(role => role.Name)));
        Assert.That(actual.Select(role => role.AttributeFullName), Is.EqualTo(expected.Select(role => role.Fqn)));
        Assert.That(actual.Select(role => role.Scopes.ToArray()),
            Is.EqualTo(expected.Select(role => role.Scopes)));
    }

    [TestCase("0.10.0", true)]
    [TestCase("0.10.7", true)]
    [TestCase("0.10.0+build.4", true)]
    [TestCase("0.10.0-alpha", false)]
    [TestCase("0.9.9", false)]
    [TestCase("0.11.0", false)]
    [TestCase("0.10", false)]
    [TestCase("v0.10.0", false)]
    [TestCase("0.10.0+", false)]
    public void IsSupportedPackageVersion_UsesManifestRangeAndSemVerSyntax(string version, bool expected)
    {
        Assert.That(CanonicalAnnotationCatalog.Current.IsSupportedPackageVersion(version), Is.EqualTo(expected));
    }

    [Test]
    public void ReservedMappingValidator_RejectsKnownOrUnknownReservedFqnsInBothCollections()
    {
        foreach ((string collection, string fqn) in new[]
                 {
                     ("attributes", "ArchLinterNet.Annotations.DomainLayerAttribute"),
                     ("attributes", "ArchLinterNet.Annotations.FutureAttribute"),
                     ("assembly_attributes", "ArchLinterNet.Annotations.DomainLayerAttribute"),
                     ("assembly_attributes", "ArchLinterNet.Annotations.FutureAttribute")
                 })
        {
            ArchitectureContractDocument document = new();
            List<ArchitectureAttributeClassificationMapping> mappings = collection == "attributes"
                ? document.Classification.Attributes
                : document.Classification.AssemblyAttributes;
            mappings.Add(new ArchitectureAttributeClassificationMapping { Attribute = fqn, Role = "DomainLayer" });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new ReservedCanonicalAnnotationMappingValidator().Validate(document))!;

            Assert.That(exception.Message, Does.Contain($"classification.{collection}[0].attribute")
                .And.Contain("reserved"));
        }
    }

    [Test]
    public void ReservedMappingValidator_AllowsUserOwnedExactFqnMappings()
    {
        ArchitectureContractDocument document = new();
        document.Classification.Attributes.Add(new ArchitectureAttributeClassificationMapping
        {
            Attribute = "Contoso.Architecture.DomainMarkerAttribute",
            Role = "DomainLayer"
        });

        Assert.DoesNotThrow(() => new ReservedCanonicalAnnotationMappingValidator().Validate(document));
    }
}
