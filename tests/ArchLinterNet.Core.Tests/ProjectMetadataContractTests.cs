using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

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

        Assert.That(violations.Select(v => v.ProjectMetadataKind), Is.EquivalentTo(new[]
        {
            "required_property",
            "forbidden_property",
            "friend_assembly",
            "project_reference"
        }));
        Assert.That(violations.Any(v => v.ProjectMetadataKey == "IsPackable" && v.ProjectMetadataExpectedValue == "true" && v.ProjectMetadataActualValue == "false"), Is.True);
        Assert.That(violations.Any(v => v.ProjectMetadataActualValue == "MyApp.Tools"), Is.True);
        Assert.That(violations.Any(v => v.ProjectMetadataActualValue == "tests/MyApp.Tests/MyApp.Tests.csproj"), Is.True);
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
        var registry = new ArchitectureContractHandlerRegistry(new IArchitectureContractHandler[]
        {
            new ProjectMetadataContractHandler()
        });

        ArchitectureContractExecutionResult strictResult = executor.Execute(strictSession, "strict", registry);
        ArchitectureContractExecutionResult auditResult = executor.Execute(auditSession, "audit", registry);

        Assert.That(strictResult.Violations, Is.Empty);
        Assert.That(auditResult.Violations, Has.Count.EqualTo(1));
    }
}
