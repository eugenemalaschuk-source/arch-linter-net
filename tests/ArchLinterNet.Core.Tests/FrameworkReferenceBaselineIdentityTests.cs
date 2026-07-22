using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

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

    [Test]
    public void SameFrameworkName_DifferentCondition_ProduceDistinctLiveIdentities()
    {
        const string SourceAssemblyName = "MyApp.Api";

        var contract = new ArchitectureFrameworkReferenceContract
        {
            Id = "domain-no-aspnet",
            Name = "Api must not reference ASP.NET Core",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_web" }
        };

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            FrameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
            {
                ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract> { contract }
            }
        };

        ProjectDiscoveryResult discovery = new(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject(
                    $"src/{SourceAssemblyName}/{SourceAssemblyName}.csproj",
                    SourceAssemblyName,
                    new[] { "net10.0", "net472" },
                    Array.Empty<ArchitectureDiscoveredPackageReference>(),
                    new[]
                    {
                        new ArchitectureDiscoveredFrameworkReference(
                            "Microsoft.AspNetCore.App", "'$(TargetFramework)'=='net10.0'"),
                        new ArchitectureDiscoveredFrameworkReference(
                            "Microsoft.AspNetCore.App", "'$(TargetFramework)'=='net472'"),
                    })
            }
        };
        var context = new ArchitectureAnalysisContext(
            "/tmp", new[] { typeof(FrameworkReferenceBaselineIdentityTests).Assembly },
            Array.Empty<string>(), Array.Empty<string>(), projectDiscovery: discovery);

        var runner = new ArchitectureContractRunner(context, document);
        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        // Regression coverage: before condition was threaded into live identity, both conditional
        // declarations produced the SAME zeroed identity and were distinguished only by call-order
        // Occurrence, so removing/reordering one baselined declaration could silently reassign the
        // suppression to the other. Both declarations must now differ by TargetMember (condition),
        // independent of Occurrence.
        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(runner.BaselineCandidates, Has.Count.EqualTo(2));

        ArchitectureViolationIdentity identityA = runner.BaselineCandidates[0].Identity!;
        ArchitectureViolationIdentity identityB = runner.BaselineCandidates[1].Identity!;

        Assert.That(identityA, Is.Not.EqualTo(identityB));
        Assert.That(identityA with { Occurrence = 0 }, Is.Not.EqualTo(identityB with { Occurrence = 0 }),
            "Identities must differ by condition-qualified TargetMember, not merely by Occurrence.");
    }

    [Test]
    public void Compare_BaselineOnlyRecordsFirstCondition_SecondConditionRemainsNewNotFrozen()
    {
        // Mirrors the same-named-type-in-different-assembly regression pattern: a baseline that only
        // ever recorded one occurrence must not suppress a distinct occurrence that happens to share a
        // display name - here, two FrameworkReference declarations under different MSBuild conditions.
        ArchitectureContractDocument policy = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract>
                {
                    new() { Id = "domain-no-aspnet", Name = "domain-no-aspnet", Source = "MyApp.Api" },
                },
            },
        };

        ArchitectureBaselineDocument baseline = new()
        {
            Version = 2,
            Baseline = new ArchitectureBaselineContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "domain-no-aspnet",
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "MyApp.Api",
                                ForbiddenReference = "Microsoft.AspNetCore.App (Condition: '$(TargetFramework)'=='net10.0')",
                                Reason = "known debt",
                                IdentityVersion = 2,
                                ContractFamily = "framework_dependency",
                                Kind = "package",
                                SourceAssembly = "MyApp.Api",
                                TargetMember = "Microsoft.AspNetCore.App (Condition: '$(TargetFramework)'=='net10.0')",
                                Occurrence = 0,
                            },
                        },
                    },
                },
            },
        };

        IReadOnlyList<ArchitectureBaselineCandidate> candidates =
        [
            new("strict_framework_dependency", "domain-no-aspnet", "MyApp.Api",
                "Microsoft.AspNetCore.App (Condition: '$(TargetFramework)'=='net10.0')",
                new ArchitectureViolationIdentity(2, "framework_dependency", "package", "domain-no-aspnet",
                    "MyApp.Api", "MyApp.Api", null, null, null,
                    "Microsoft.AspNetCore.App (Condition: '$(TargetFramework)'=='net10.0')", 0)),
            new("strict_framework_dependency", "domain-no-aspnet", "MyApp.Api",
                "Microsoft.AspNetCore.App (Condition: '$(TargetFramework)'=='net472')",
                new ArchitectureViolationIdentity(2, "framework_dependency", "package", "domain-no-aspnet",
                    "MyApp.Api", "MyApp.Api", null, null, null,
                    "Microsoft.AspNetCore.App (Condition: '$(TargetFramework)'=='net472')", 0)),
        ];

        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            policy, baseline, candidates, mode: "all");

        Assert.Multiple(() =>
        {
            Assert.That(result.Frozen, Has.Count.EqualTo(1));
            Assert.That(result.Frozen[0].ForbiddenReference, Does.Contain("net10.0"));
            Assert.That(result.New, Has.Count.EqualTo(1));
            Assert.That(result.New[0].ForbiddenReference, Does.Contain("net472"));
        });
    }
}
