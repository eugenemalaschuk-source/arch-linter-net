using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ProjectMetadataContractTests
{
    private static ArchitectureAnalysisContext CreateContext(params ArchitectureDiscoveredProject[] discoveredProjects)
    {
        ProjectDiscoveryResult discovery = new(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = discoveredProjects
        };

        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ProjectMetadataContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
    }

    private static ArchitectureDiscoveredProject Project(
        string path,
        params (string Key, string Value, string SourcePath)[] properties)
    {
        return new ArchitectureDiscoveredProject(
            path,
            Path.GetFileNameWithoutExtension(path),
            new[] { "net10.0" },
            Array.Empty<ArchitectureDiscoveredPackageReference>(),
            Array.Empty<ArchitectureDiscoveredFrameworkReference>(),
            properties.ToDictionary(
                entry => entry.Key,
                entry => new ArchitectureDiscoveredProjectProperty(entry.Key, entry.Value, entry.SourcePath),
                StringComparer.OrdinalIgnoreCase),
            new[]
            {
                new ArchitectureDiscoveredFriendAssembly("MyApp.Tests", path),
                new ArchitectureDiscoveredFriendAssembly("MyApp.Tools", path)
            },
            new[]
            {
                new ArchitectureDiscoveredProjectReference("tests/MyApp.Tests/MyApp.Tests.csproj", path),
                new ArchitectureDiscoveredProjectReference("src/MyApp.Abstractions/MyApp.Abstractions.csproj", path)
            });
    }

    private static ArchitectureContractDocument CreateDocument(ArchitectureProjectMetadataContract contract, bool audit = false)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = audit ? new List<ArchitectureProjectMetadataContract>() : new List<ArchitectureProjectMetadataContract> { contract },
                AuditProjectMetadata = audit ? new List<ArchitectureProjectMetadataContract> { contract } : new List<ArchitectureProjectMetadataContract>()
            }
        };
    }

    [Test]
    public void CheckProjectMetadataContract_ReportsRequiredForbiddenFriendAndProjectReferenceViolations()
    {
        const string ProjectPath = "src/MyApp/MyApp.csproj";
        ArchitectureProjectMetadataContract contract = new()
        {
            Name = "project-metadata",
            Id = "project-metadata",
            Projects = new List<string> { ProjectPath },
            RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nullable"] = "enable",
                ["IsPackable"] = "true"
            },
            ForbiddenProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsTestProject"] = "true"
            },
            AllowedFriendAssemblies = new List<string> { "MyApp.Tests" },
            ForbiddenProjectReferences = new List<string> { "tests/**/*.csproj" }
        };

        ArchitectureContractRunner runner = new(
            CreateContext(Project(
                ProjectPath,
                ("Nullable", "enable", ProjectPath),
                ("IsPackable", "false", ProjectPath),
                ("IsTestProject", "true", ProjectPath))),
            CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckProjectMetadataContract(contract);

        Assert.That(violations.Select(v => (v.Payload as ProjectMetadataPayload)?.ProjectMetadataKind), Is.EquivalentTo(new[]
        {
            "required_property",
            "forbidden_property",
            "friend_assembly",
            "project_reference"
        }));
        Assert.That(violations.Any(v => (v.Payload as ProjectMetadataPayload)?.ProjectMetadataKey == "IsPackable" && (v.Payload as ProjectMetadataPayload)?.ProjectMetadataExpectedValue == "true" && (v.Payload as ProjectMetadataPayload)?.ProjectMetadataActualValue == "false"), Is.True);
        Assert.That(violations.Any(v => (v.Payload as ProjectMetadataPayload)?.ProjectMetadataActualValue == "MyApp.Tools"), Is.True);
        Assert.That(violations.Any(v => (v.Payload as ProjectMetadataPayload)?.ProjectMetadataActualValue == "tests/MyApp.Tests/MyApp.Tests.csproj"), Is.True);
    }

    [Test]
    public void AuditProjectMetadataViolation_IsExecutedOnlyInAuditMode()
    {
        const string ProjectPath = "src/MyApp/MyApp.csproj";
        ArchitectureProjectMetadataContract contract = new()
        {
            Name = "audit-project-metadata",
            Id = "audit-project-metadata",
            Projects = new List<string> { ProjectPath },
            RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsPackable"] = "true"
            }
        };

        ArchitectureContractDocument document = CreateDocument(contract, audit: true);
        ArchitectureAnalysisSession strictSession = new(
            CreateContext(Project(ProjectPath, ("IsPackable", "false", ProjectPath))),
            document,
            selectedContractIds: null,
            enableUnmatchedIgnoreTracking: false,
            preprocessorSymbols: null);
        ArchitectureAnalysisSession auditSession = new(
            CreateContext(Project(ProjectPath, ("IsPackable", "false", ProjectPath))),
            document,
            selectedContractIds: null,
            enableUnmatchedIgnoreTracking: false,
            preprocessorSymbols: null);

        var executor = new ArchitectureContractExecutor();
        var registry = new ArchitectureContractHandlerRegistry();

        ArchitectureContractExecutionResult strictResult = executor.Execute(strictSession, "strict", registry);
        ArchitectureContractExecutionResult auditResult = executor.Execute(auditSession, "audit", registry);

        Assert.That(strictResult.Violations, Is.Empty);
        Assert.That(auditResult.Violations, Has.Count.EqualTo(1));
    }

    [Test]
    public void CheckProjectMetadataContract_IgnoredViolationsSupportBaselineStyleSuppression()
    {
        const string ProjectPath = "src/MyApp/MyApp.csproj";
        ArchitectureProjectMetadataContract contract = new()
        {
            Name = "project-metadata",
            Id = "project-metadata",
            Projects = new List<string> { ProjectPath },
            RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsPackable"] = "true"
            },
            AllowedFriendAssemblies = new List<string> { "MyApp.Tests" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = ProjectPath,
                    ForbiddenReference = "friend_assembly:MyApp.Tools",
                    Reason = "known debt"
                }
            }
        };

        ArchitectureContractRunner runner = new(
            CreateContext(Project(
                ProjectPath,
                ("IsPackable", "false", ProjectPath))),
            CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckProjectMetadataContract(contract);

        Assert.That(violations.Select(v => (v.Payload as ProjectMetadataPayload)?.ProjectMetadataKind), Is.EquivalentTo(new[]
        {
            "required_property"
        }));
        Assert.That(violations.Any(v => (v.Payload as ProjectMetadataPayload)?.ProjectMetadataActualValue == "MyApp.Tools"), Is.False);
        Assert.That(runner.BaselineCandidates.Any(candidate =>
            candidate.ContractGroup == "strict_project_metadata"
            && candidate.ContractId == "project-metadata"
            && candidate.SourceType == ProjectPath
            && candidate.ForbiddenReference == "required_property:IsPackable=false"), Is.True);
        Assert.That(runner.UnmatchedIgnoredViolations, Is.Empty);
    }

    [Test]
    public void CheckProjectMetadataContract_EmptyFriendAllowlist_DeniesAllFriendAssemblies()
    {
        const string ProjectPath = "src/MyApp/MyApp.csproj";
        ArchitectureProjectMetadataContract contract = new()
        {
            Name = "no-friends",
            Id = "no-friends",
            Projects = new List<string> { ProjectPath },
            AllowedFriendAssemblies = new List<string>()
        };

        ArchitectureContractRunner runner = new(
            CreateContext(Project(ProjectPath)),
            CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckProjectMetadataContract(contract);

        Assert.That(violations.Count, Is.EqualTo(2));
        Assert.That(violations.All(v => (v.Payload as ProjectMetadataPayload)?.ProjectMetadataKind == "friend_assembly"), Is.True);
        Assert.That(violations.Select(v => (v.Payload as ProjectMetadataPayload)?.ProjectMetadataActualValue),
            Is.EquivalentTo(new[] { "MyApp.Tests", "MyApp.Tools" }));
        Assert.That(violations.Any(v => v.ForbiddenReferences.First().Contains("deny-all")), Is.True);
    }

    [Test]
    public void CheckProjectMetadataContract_NullFriendAllowlist_SkipsFriendCheck()
    {
        const string ProjectPath = "src/MyApp/MyApp.csproj";
        ArchitectureProjectMetadataContract contract = new()
        {
            Name = "no-friend-expectation",
            Id = "no-friend-expectation",
            Projects = new List<string> { ProjectPath },
            RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nullable"] = "enable"
            }
        };

        ArchitectureContractRunner runner = new(
            CreateContext(Project(ProjectPath, ("Nullable", "enable", ProjectPath))),
            CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckProjectMetadataContract(contract);

        Assert.That(violations.All(v => (v.Payload as ProjectMetadataPayload)?.ProjectMetadataKind != "friend_assembly"), Is.True);
    }

    [Test]
    public void CheckProjectMetadataContract_EmptyFriendAllowlist_SupportsBaselineSuppression()
    {
        const string ProjectPath = "src/MyApp/MyApp.csproj";
        ArchitectureProjectMetadataContract contract = new()
        {
            Name = "no-friends-baselined",
            Id = "no-friends-baselined",
            Projects = new List<string> { ProjectPath },
            AllowedFriendAssemblies = new List<string>(),
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new()
                {
                    SourceType = ProjectPath,
                    ForbiddenReference = "friend_assembly:MyApp.Tools",
                    Reason = "known debt"
                }
            }
        };

        ArchitectureContractRunner runner = new(
            CreateContext(Project(ProjectPath)),
            CreateDocument(contract));

        List<ArchitectureViolation> violations = runner.Session.CheckProjectMetadataContract(contract);

        Assert.That(violations.Count, Is.EqualTo(1));
        Assert.That((violations[0].Payload as ProjectMetadataPayload)?.ProjectMetadataActualValue, Is.EqualTo("MyApp.Tests"));
        Assert.That(runner.BaselineCandidates.Any(candidate =>
            candidate.ContractGroup == "strict_project_metadata"
            && candidate.ContractId == "no-friends-baselined"
            && candidate.SourceType == ProjectPath
            && candidate.ForbiddenReference == "friend_assembly:MyApp.Tests"), Is.True);
    }
}
