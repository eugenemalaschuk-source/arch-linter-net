using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class FrameworkReferenceBaselineIdentityTests
{
    [Test]
    public void ResolveKind_FrameworkDependency_ResolvesToPackageBucket()
    {
        Assert.That(ArchitectureViolationIdentity.ResolveKind("framework_dependency"), Is.EqualTo("package"));
    }

    [Test]
    public void ResolveKind_FrameworkAllowOnly_ResolvesToPackageBucket()
    {
        Assert.That(ArchitectureViolationIdentity.ResolveKind("framework_allow_only"), Is.EqualTo("package"));
    }

    [Test]
    public void ResolveContractFamily_StrictAndAuditFrameworkGroups_StripModePrefix()
    {
        Assert.That(ArchitectureViolationIdentity.ResolveContractFamily("strict_framework_dependency"),
            Is.EqualTo("framework_dependency"));
        Assert.That(ArchitectureViolationIdentity.ResolveContractFamily("audit_framework_allow_only"),
            Is.EqualTo("framework_allow_only"));
    }

    [Test]
    public void SameFrameworkName_DifferentSourceProjects_ProduceDistinctIdentities()
    {
        var identityA = new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion, "framework_dependency", "package", "domain-no-aspnet",
            "MyApp.Api", "MyApp.Api", null, null, null, null, 0);
        var identityB = identityA with { SourceAssembly = "MyApp.Worker", SourceType = "MyApp.Worker" };

        Assert.That(identityA, Is.Not.EqualTo(identityB));
    }

    [Test]
    public void SameFrameworkName_DifferentCondition_ProduceDistinctLegacyPairs()
    {
        var identity = new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion, "framework_dependency", "package", "domain-no-aspnet",
            "MyApp.Api", "MyApp.Api", null, null, null, null, 0);

        (string SourceType, string ForbiddenReference) pairA = identity.ToLegacyPair(
            "Microsoft.AspNetCore.App (Condition: '$(TargetFramework)'=='net10.0')");
        (string SourceType, string ForbiddenReference) pairB = identity.ToLegacyPair(
            "Microsoft.AspNetCore.App (Condition: '$(TargetFramework)'=='net472')");

        Assert.That(pairA, Is.Not.EqualTo(pairB));
    }
}
