using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSessionMetadataIndexesTests
{
    [Test]
    public void ManyProjectsAndContracts_MaterializeEachSessionMetadataIndexOnce()
    {
        const int ProjectCount = 24;
        const int ContractCount = 16;
        ArchitectureDiscoveredProject[] projects = Enumerable.Range(0, ProjectCount)
            .Select(index => Project(index, packageId: "Forbidden.Package", nullable: "enable"))
            .Append(new ArchitectureDiscoveredProject(
                "src/Project00/Project00.csproj",
                "Project00",
                new[] { "net10.0" },
                new[] { new ArchitectureDiscoveredPackageReference("Second.Entry", "1.0.0") }))
            .ToArray();
        ArchitectureAnalysisContext context = CreateContext(projects);
        ArchitectureAnalysisSession session = new(
            context,
            CreateDocument(),
            selectedContractIds: null,
            enableUnmatchedIgnoreTracking: false,
            preprocessorSymbols: null);

        foreach (int index in Enumerable.Range(0, ContractCount))
        {
            string assemblyName = ProjectName(index);
            List<ArchitectureViolation> frameworkViolations = session.CheckFrameworkDependencyContract(
                new ArchitectureFrameworkReferenceContract
                {
                    Name = $"{assemblyName} framework check",
                    Source = $"Missing{assemblyName}",
                    Forbidden = new List<string> { "forbidden-framework" },
                });

            Assert.That(frameworkViolations, Is.Empty);
        }

        // Framework contracts are the first users of the project metadata facts. Keep this
        // assertion before package and project-metadata checks so this test fails if the framework
        // lookup regresses to a per-contract discovery scan instead of using the session index.
        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));

        string testAssemblyName = typeof(ArchitectureSessionMetadataIndexesTests).Assembly.GetName().Name!;
        foreach (int index in Enumerable.Range(0, ContractCount))
        {
            List<ArchitectureViolation> assemblyViolations = session.CheckAssemblyDependencyContract(
                new ArchitectureAssemblyDependencyContract
                {
                    Name = $"assembly check {index:D2}",
                    Source = testAssemblyName,
                    Forbidden = new List<string> { testAssemblyName },
                });

            Assert.That(assemblyViolations, Is.Empty);
        }

        Assert.That(context.ProfilingCounters.SessionAssemblyIndexMaterializations, Is.EqualTo(1));

        foreach (int index in Enumerable.Range(0, ContractCount))
        {
            string assemblyName = ProjectName(index);
            List<ArchitectureViolation> packageViolations = session.CheckPackageDependencyContract(
                new ArchitecturePackageDependencyContract
                {
                    Name = $"{assemblyName} package check",
                    Source = assemblyName,
                    Forbidden = new List<string> { "forbidden" },
                });
            List<ArchitectureViolation> metadataViolations = session.CheckProjectMetadataContract(
                new ArchitectureProjectMetadataContract
                {
                    Name = $"{assemblyName} metadata check",
                    Projects = new List<string> { $"src/{assemblyName}/{assemblyName}.csproj" },
                    RequiredProperties = new Dictionary<string, string> { ["Nullable"] = "enable" },
                });

            Assert.That(packageViolations, Has.Count.EqualTo(1));
            Assert.That(packageViolations[0].SourceType, Is.EqualTo(assemblyName));
            Assert.That(metadataViolations, Is.Empty);
        }

        Assert.That(
            session.Facts.TryGetProjectByAssemblyName("Project00", out ArchitectureDiscoveredProject? firstAssemblyOwner),
            Is.True);
        Assert.That(firstAssemblyOwner!.PackageReferences.Single().PackageId, Is.EqualTo("Forbidden.Package"));
        Assert.That(
            session.Facts.TryGetProjectByNormalizedPath(
                ProjectPathNormalizer.Normalize(@"src\Project00\Project00.csproj"),
                out ArchitectureDiscoveredProject? firstPathOwner),
            Is.True);
        Assert.That(firstPathOwner!.PackageReferences.Single().PackageId, Is.EqualTo("Forbidden.Package"));
        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));
        Assert.That(context.ProfilingCounters.SessionAssemblyIndexMaterializations, Is.EqualTo(1));
    }

    private static ArchitectureAnalysisContext CreateContext(IReadOnlyList<ArchitectureDiscoveredProject> projects)
    {
        ProjectDiscoveryResult discovery = new(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = projects,
        };

        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(ArchitectureSessionMetadataIndexesTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
    }

    private static ArchitectureContractDocument CreateDocument()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Session metadata indexes",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration(),
            Packages = new Dictionary<string, ArchitecturePackageGroup>
            {
                ["forbidden"] = new ArchitecturePackageGroup { PackageIds = { "Forbidden.Package" } },
            },
            FrameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
            {
                ["forbidden-framework"] = new ArchitectureFrameworkReferenceGroup(),
            },
            Contracts = new ArchitectureContractGroups(),
        };
    }

    private static ArchitectureDiscoveredProject Project(int index, string packageId, string nullable)
    {
        string name = ProjectName(index);
        return new ArchitectureDiscoveredProject(
            $"src/{name}/{name}.csproj",
            name,
            new[] { "net10.0" },
            new[] { new ArchitectureDiscoveredPackageReference(packageId, "1.0.0") })
        {
            Properties = new Dictionary<string, ArchitectureDiscoveredProjectProperty>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nullable"] = new("Nullable", nullable, $"src/{name}/{name}.csproj"),
            },
        };
    }

    private static string ProjectName(int index) => $"Project{index:D2}";
}
