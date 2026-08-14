using System.Text.Json;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AiCapabilityManifestParityTests
{
    [Test]
    public void ContractFamilyGroups_MatchRuntimeRegistryExactly()
    {
        using JsonDocument manifest = ReadManifest();

        string[] manifestGroups = manifest.RootElement
            .GetProperty("contractFamilies")
            .EnumerateArray()
            .Select(family =>
                $"{family.GetProperty("strictGroup").GetString()}|{family.GetProperty("auditGroup").GetString()}")
            .ToArray();

        string[] runtimeGroups = ArchitectureContractFamilyRegistry.All
            .Select(family => $"{family.StrictGroupName}|{family.AuditGroupName}")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(manifestGroups, Is.Unique,
                "Each strict/audit group pair must be represented once in the AI capability manifest.");
            Assert.That(manifestGroups, Is.EquivalentTo(runtimeGroups),
                "The AI capability manifest must cover every runtime contract family exactly once.");
        });
    }

    [Test]
    public void ProjectSourceSets_DoNotAdvertiseThePre465ExplicitMembersOnlyLimitation()
    {
        using JsonDocument manifest = ReadManifest();
        JsonElement sourceSets = manifest.RootElement.GetProperty("sourceSets");

        string projectUniverse = sourceSets
            .GetProperty("globUniverse")
            .GetProperty("project")
            .GetString()!;
        string[] unsupported = sourceSets
            .GetProperty("unsupported")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(projectUniverse, Does.Contain("analysis.solution"),
                "Project source sets must advertise the solution-discovered project universe delivered by #465.");
            Assert.That(projectUniverse, Does.Contain("**"),
                "Project source sets must advertise repository-relative recursive path globs delivered by #465.");
            Assert.That(unsupported, Does.Not.Contain("globs for kind: project"),
                "Project globs are supported after #465 and must not be advertised as unsupported.");
        });
    }

    [Test]
    public void PublicApiSurface_AdvertisesIntentionalSurfaceSelectorEvidence()
    {
        using JsonDocument manifest = ReadManifest();

        JsonElement publicApi = manifest.RootElement
            .GetProperty("contractFamilies")
            .EnumerateArray()
            .Single(family => family.GetProperty("strictGroup").GetString() == "strict_public_api_surface");
        string[] fields = publicApi
            .GetProperty("fields")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(fields, Does.Contain("surface_selector.has_attribute"));
            Assert.That(fields, Does.Contain("surface_selector.role"));
            Assert.That(publicApi.GetProperty("validates").GetString(), Does.Contain("intentional reviewed subset"));
        });
    }

    private static JsonDocument ReadManifest()
    {
        string repositoryRoot = SelfPolicyRepository.FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot, "archlinternet.capabilities.json");
        return JsonDocument.Parse(File.ReadAllText(manifestPath));
    }
}
