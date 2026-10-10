using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CanonicalAnnotationCapabilityMetadataTests
{
    [Test]
    public void SemanticAnnotationProjection_MatchesCanonicalManifest()
    {
        using JsonDocument manifest = Read("architecture/semantic-annotations.v1.json");
        using JsonDocument capabilities = Read("archlinternet.capabilities.json");

        JsonElement manifestRoot = manifest.RootElement;
        JsonElement projection = capabilities.RootElement.GetProperty("semanticAnnotations");
        JsonElement package = manifestRoot.GetProperty("package");
        JsonElement marker = manifestRoot.GetProperty("catalogIdentityMarker");

        Assert.Multiple(() =>
        {
            Assert.That(projection.GetProperty("reservedNamespace").GetString(),
                Is.EqualTo(manifestRoot.GetProperty("reservedNamespace").GetString()));
            Assert.That(projection.GetProperty("catalogGeneration").GetInt32(),
                Is.EqualTo(manifestRoot.GetProperty("catalogGeneration").GetInt32()));
            Assert.That(projection.GetProperty("packageId").GetString(),
                Is.EqualTo(package.GetProperty("id").GetString()));
            Assert.That(projection.GetProperty("minimumToolVersion").GetString(),
                Is.EqualTo(package.GetProperty("minimumAnnotationAwareToolVersion").GetString()));
            Assert.That(projection.GetProperty("supportedPackageRanges").EnumerateArray()
                .Select(value => value.GetString()).ToArray(),
                Is.EqualTo(new[] { package.GetProperty("compatiblePackageRangeForGeneration1").GetString() }));
            Assert.That(projection.GetProperty("catalogIdentityMarker").GetProperty("fqn").GetString(),
                Is.EqualTo(marker.GetProperty("fqn").GetString()));
            Assert.That(projection.GetProperty("catalogIdentityMarker").GetProperty("scope").GetString(),
                Is.EqualTo(marker.GetProperty("attributeTargets")[0].GetString()));
            Assert.That(projection.GetProperty("catalogIdentityMarker").GetProperty("catalogGeneration").GetInt32(),
                Is.EqualTo(marker.GetProperty("value").GetProperty("catalogGeneration").GetInt32()));
            Assert.That(projection.GetProperty("catalogIdentityMarker").GetProperty("packageVersion").GetString(),
                Is.EqualTo(marker.GetProperty("value").GetProperty("packageVersion").GetString()));
        });

        JsonElement[] manifestRoles = manifestRoot.GetProperty("roles").EnumerateArray().ToArray();
        JsonElement[] projectedRoles = projection.GetProperty("roleAnnotations").EnumerateArray().ToArray();
        Assert.That(projectedRoles, Has.Length.EqualTo(manifestRoles.Length));

        for (int index = 0; index < manifestRoles.Length; index++)
        {
            Assert.That(projectedRoles[index].GetProperty("role").GetString(),
                Is.EqualTo(manifestRoles[index].GetProperty("role").GetString()));
            Assert.That(projectedRoles[index].GetProperty("fqn").GetString(),
                Is.EqualTo(manifestRoles[index].GetProperty("fqn").GetString()));
            Assert.That(projectedRoles[index].GetProperty("scopes").EnumerateArray()
                .Select(value => value.GetString()).ToArray(),
                Is.EqualTo(manifestRoles[index].GetProperty("scopes").EnumerateArray()
                    .Select(value => value.GetString()).ToArray()));
        }
    }

    [Test]
    public void SemanticAnnotationProjection_ExcludesNonRoleAndMetadataOnlyIdentities()
    {
        using JsonDocument manifest = Read("architecture/semantic-annotations.v1.json");
        using JsonDocument capabilities = Read("archlinternet.capabilities.json");

        JsonElement root = manifest.RootElement;
        JsonElement projection = capabilities.RootElement.GetProperty("semanticAnnotations");
        HashSet<string> projectedRoles = projection.GetProperty("roleAnnotations").EnumerateArray()
            .Select(role => role.GetProperty("role").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> contextAttributes = root.GetProperty("contextAttributes").EnumerateArray()
            .Select(attribute => attribute.GetProperty("fqn").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> customMappingOnly = root.GetProperty("annotationDisposition")
            .GetProperty("customMappingOnly").EnumerateArray()
            .Select(role => role.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> deferred = root.GetProperty("annotationDisposition")
            .GetProperty("deferred").EnumerateArray()
            .Select(role => role.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> projectedFqns = projection.GetProperty("roleAnnotations").EnumerateArray()
            .Select(role => role.GetProperty("fqn").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(projectedRoles.Intersect(customMappingOnly), Is.Empty);
            Assert.That(projectedRoles.Intersect(deferred), Is.Empty);
            Assert.That(projectedFqns.Intersect(contextAttributes), Is.Empty);
            Assert.That(projection.GetProperty("policyEffects")
                .GetProperty("metadataOnlyContextAttributesAreNotRoleAnnotations").GetBoolean(), Is.True);
            Assert.That(projection.GetProperty("policyEffects")
                .GetProperty("customMappingOnlyRolesAreNotRoleAnnotations").GetBoolean(), Is.True);
            Assert.That(projection.GetProperty("policyEffects")
                .GetProperty("deferredRolesAreNotRoleAnnotations").GetBoolean(), Is.True);
            Assert.That(projection.GetProperty("compatibilityDiagnostics")
                .GetProperty("failClosed").GetBoolean(), Is.True);
        });
    }

    private static JsonDocument Read(string relativePath)
    {
        string repositoryRoot = SelfPolicyRepository.FindRepositoryRoot();
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, relativePath)));
    }
}
