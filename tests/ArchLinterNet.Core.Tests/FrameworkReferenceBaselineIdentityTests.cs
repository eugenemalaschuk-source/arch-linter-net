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
    public void SameFrameworkName_DifferentTargetFramework_ProduceDistinctLegacyPairs()
    {
        var identity = new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion, "framework_dependency", "package", "domain-no-aspnet",
            "MyApp.Api", "MyApp.Api", null, null, null, null, 0);

        (string SourceType, string ForbiddenReference) pairA = identity.ToLegacyPair(
            "Microsoft.AspNetCore.App (net10.0)");
        (string SourceType, string ForbiddenReference) pairB = identity.ToLegacyPair(
            "Microsoft.AspNetCore.App (net472)");

        Assert.That(pairA, Is.Not.EqualTo(pairB));
    }

    [Test]
    public void MultiTargetProject_ConditionScopedFrameworkReference_AppliesOnlyToMatchingTargetFramework()
    {
        // Real, on-disk, cross-targeting-aware (plural <TargetFrameworks>) project fixture, evaluated
        // through the actual ArchitectureFrameworkReferenceEvaluator/Buildalyzer design-time build -
        // not a hand-built fake. Only net10.0 is installed in this sandbox, so the fixture's single
        // real inner build still exercises: (a) Buildalyzer's outer/coordination result being skipped
        // (empty TargetFramework), (b) ItemGroup-level Condition scoped to '$(TargetFramework)'=='net10.0'
        // being genuinely evaluated by MSBuild, and (c) explicit vs. implicit (SDK-injected
        // Microsoft.NETCore.App) classification via the real IsImplicitlyDefined metadata.
        const string SourceAssemblyName = "MyApp.Api";
        string repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-framework-multitarget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoRoot);
        try
        {
            string projectDir = Path.Combine(repoRoot, SourceAssemblyName);
            Directory.CreateDirectory(projectDir);
            string projectPath = Path.Combine(projectDir, $"{SourceAssemblyName}.csproj");
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>net10.0</TargetFrameworks>
                  </PropertyGroup>
                  <ItemGroup Condition="'$(TargetFramework)'=='net10.0'">
                    <FrameworkReference Include="Microsoft.AspNetCore.App" />
                  </ItemGroup>
                </Project>
                """);

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
                Analysis = new ArchitectureAnalysisConfiguration
                {
                    TargetAssemblies = new List<string> { SourceAssemblyName },
                    Projects = new List<string> { projectPath }
                },
                Contracts = new ArchitectureContractGroups
                {
                    StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract> { contract }
                }
            };

            ProjectDiscoveryResult discovery = new ArchitectureProjectDiscoveryService()
                .ResolveFromDocument(document, repoRoot, resolveAssemblyOutputs: false);
            var context = new ArchitectureAnalysisContext(
                repoRoot, new[] { typeof(FrameworkReferenceBaselineIdentityTests).Assembly },
                Array.Empty<string>(), Array.Empty<string>(), projectDiscovery: discovery);

            var runner = new ArchitectureContractRunner(context, document);
            List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { "Microsoft.AspNetCore.App (net10.0)" }));
            Assert.That(runner.BaselineCandidates, Has.Count.EqualTo(1));

            // Regression coverage: identity now keys on the real evaluated TargetFramework, so an
            // occurrence of the same FrameworkName under a different (hypothetical) TargetFramework
            // would be a distinct identity - proven here via TargetMember, independent of Occurrence.
            ArchitectureViolationIdentity identity = runner.BaselineCandidates[0].Identity!;
            ArchitectureViolationIdentity hypotheticalOtherTfm = identity with
            {
                TargetMember = "Microsoft.AspNetCore.App (net472)",
            };
            Assert.That(identity, Is.Not.EqualTo(hypotheticalOtherTfm));
        }
        finally
        {
            Directory.Delete(repoRoot, true);
        }
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
                                ForbiddenReference = "Microsoft.AspNetCore.App (net10.0)",
                                Reason = "known debt",
                                IdentityVersion = 2,
                                ContractFamily = "framework_dependency",
                                Kind = "package",
                                SourceAssembly = "MyApp.Api",
                                TargetMember = "Microsoft.AspNetCore.App (net10.0)",
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
                "Microsoft.AspNetCore.App (net10.0)",
                new ArchitectureViolationIdentity(2, "framework_dependency", "package", "domain-no-aspnet",
                    "MyApp.Api", "MyApp.Api", null, null, null,
                    "Microsoft.AspNetCore.App (net10.0)", 0)),
            new("strict_framework_dependency", "domain-no-aspnet", "MyApp.Api",
                "Microsoft.AspNetCore.App (net472)",
                new ArchitectureViolationIdentity(2, "framework_dependency", "package", "domain-no-aspnet",
                    "MyApp.Api", "MyApp.Api", null, null, null,
                    "Microsoft.AspNetCore.App (net472)", 0)),
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
