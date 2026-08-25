using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

// Consumer-shaped, deterministic evidence for #652/#653. The fixture deliberately stays in
// process: the immutable discovered-project inventory and one loaded test assembly are the same
// inputs every run sees, while the executor supplies the same strict/audit family ordering that
// validation exposes to consumers.
[TestFixture]
public sealed class RepeatedWorkRegressionEvidenceTests
{
    private const int DiscoveredProjectCount = 24;
    private const int ContractFanOut = 16;
    private const string ForbiddenPackage = "Synthetic.Forbidden.Package";
    private const string AllowedPackage = "Synthetic.Allowed.Package";
    private static readonly string[] _targetFrameworks = { "net10.0" };

    [Test]
    public void ConsumerShapedFanOut_ReusesSessionMaterializationAndPreservesCanonicalModes()
    {
        ArchitectureContractDocument document = CreateDocument();
        ArchitectureAnalysisContext firstContext = CreateContext();
        ArchitectureAnalysisSession firstSession = new(firstContext, document, null, false, null);
        ArchitectureContractExecutor executor = new();
        ArchitectureContractHandlerRegistry registry = new();

        Assert.Multiple(() =>
        {
            Assert.That(firstContext.ProjectDiscovery!.DiscoveredProjects, Has.Count.EqualTo(DiscoveredProjectCount));
            Assert.That(firstSession.Catalog.ContractsFor("strict", "package_dependency").Count(), Is.EqualTo(ContractFanOut));
            Assert.That(firstSession.Catalog.ContractsFor("strict", "framework_dependency").Count(), Is.EqualTo(ContractFanOut));
            Assert.That(firstSession.Catalog.ContractsFor("strict", "assembly_dependency").Count(), Is.EqualTo(ContractFanOut));
            Assert.That(firstSession.Catalog.ContractsFor("strict", "project_metadata").Count(), Is.EqualTo(ContractFanOut));
        });

        ArchitectureContractExecutionResult strict = executor.Execute(firstSession, "strict", registry);
        ArchitectureContractExecutionResult audit = executor.Execute(firstSession, "audit", registry);

        IReadOnlyList<string> strictProjection = CanonicalProjection(strict, "strict");
        IReadOnlyList<string> auditProjection = CanonicalProjection(audit, "audit");

        // Strict metadata contracts all pass, including the duplicate Project00 entry: the first
        // discovered record remains authoritative for package and normalized-path lookups. Audit
        // intentionally reports the same selected API surface with no declarations, proving the
        // mode's warning/failure result is still observable without changing the shared surface.
        Assert.Multiple(() =>
        {
            Assert.That(strict.Violations, Is.Empty);
            Assert.That(strictProjection, Is.Empty);
            Assert.That(audit.Violations, Is.Not.Empty);
            Assert.That(auditProjection, Is.Not.Empty);
            Assert.That(audit.Violations.Select(v => v.ContractId).Distinct(), Is.EqualTo(new[] { "audit-public-api" }));
            Assert.That(firstSession.Facts.TryGetProjectByAssemblyName("Project00", out ArchitectureDiscoveredProject? firstAssemblyOwner), Is.True);
            Assert.That(firstAssemblyOwner!.PackageReferences.Single().PackageId, Is.EqualTo(AllowedPackage));
            Assert.That(firstSession.Facts.TryGetProjectByNormalizedPath(
                ProjectPathNormalizer.Normalize("src/Project00/Project00.csproj"),
                out ArchitectureDiscoveredProject? firstPathOwner), Is.True);
            Assert.That(firstPathOwner!.PackageReferences.Single().PackageId, Is.EqualTo(AllowedPackage));
            Assert.That(firstContext.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));
            Assert.That(firstContext.ProfilingCounters.SessionAssemblyIndexMaterializations, Is.EqualTo(1));
            Assert.That(firstSession.PublicApiSurfaceMaterializationCount, Is.EqualTo(1));
        });

        // Rebuild the equivalent immutable session and compare the consumer-facing canonical
        // identities/order. This intentionally excludes timing and allocation observations.
        ArchitectureAnalysisContext secondContext = CreateContext();
        ArchitectureAnalysisSession secondSession = new(secondContext, document, null, false, null);
        ArchitectureContractExecutionResult strictAgain = executor.Execute(secondSession, "strict", registry);
        ArchitectureContractExecutionResult auditAgain = executor.Execute(secondSession, "audit", registry);

        Assert.Multiple(() =>
        {
            Assert.That(CanonicalProjection(strictAgain, "strict"), Is.EqualTo(strictProjection));
            Assert.That(CanonicalProjection(auditAgain, "audit"), Is.EqualTo(auditProjection));
            Assert.That(secondContext.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));
            Assert.That(secondContext.ProfilingCounters.SessionAssemblyIndexMaterializations, Is.EqualTo(1));
            Assert.That(secondSession.PublicApiSurfaceMaterializationCount, Is.EqualTo(1));
        });
    }

    private static ArchitectureContractDocument CreateDocument()
    {
        string assemblyName = typeof(RepeatedWorkRegressionEvidenceTests).Assembly.GetName().Name!;
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Repeated work regression evidence",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Packages = new Dictionary<string, ArchitecturePackageGroup>
            {
                ["forbidden"] = new() { PackageIds = { ForbiddenPackage } },
            },
            FrameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
            {
                ["forbidden-framework"] = new() { FrameworkNames = { "Synthetic.Forbidden.Framework" } },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { assemblyName },
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageDependency = Enumerable.Range(0, ContractFanOut)
                    .Select(index => new ArchitecturePackageDependencyContract
                    {
                        Name = $"package-{index:D2}",
                        Id = $"package-{index:D2}",
                        Source = ProjectName(index),
                        Forbidden = new List<string> { "forbidden" },
                    })
                    .ToList(),
                StrictFrameworkDependency = Enumerable.Range(0, ContractFanOut)
                    .Select(index => new ArchitectureFrameworkReferenceContract
                    {
                        Name = $"framework-{index:D2}",
                        Id = $"framework-{index:D2}",
                        Source = ProjectName(index),
                        Forbidden = new List<string> { "forbidden-framework" },
                    })
                    .ToList(),
                StrictAssemblyDependency = Enumerable.Range(0, ContractFanOut)
                    .Select(index => new ArchitectureAssemblyDependencyContract
                    {
                        Name = $"assembly-{index:D2}",
                        Id = $"assembly-{index:D2}",
                        Source = assemblyName,
                        Forbidden = new List<string> { assemblyName },
                    })
                    .ToList(),
                StrictProjectMetadata = Enumerable.Range(0, ContractFanOut)
                    .Select(index => new ArchitectureProjectMetadataContract
                    {
                        Name = $"metadata-{index:D2}",
                        Id = $"metadata-{index:D2}",
                        Projects = new List<string> { $"src/{ProjectName(index)}/{ProjectName(index)}.csproj" },
                        RequiredProperties = new Dictionary<string, string> { ["Nullable"] = "enable" },
                    })
                    .ToList(),
                StrictPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract>
                {
                    PublicApiContract("strict-public-api", declared: true),
                },
                AuditPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract>
                {
                    PublicApiContract("audit-public-api", declared: false),
                },
            },
        };
    }

    private static ArchitecturePublicApiSurfaceContract PublicApiContract(string id, bool declared)
    {
        string typeName = "PublicApiSurfaceSelectorTestFixtures.PublicSurface.SelectedByNamespace";
        return new ArchitecturePublicApiSurfaceContract
        {
            Name = id,
            Id = id,
            Assemblies = new List<string> { typeof(RepeatedWorkRegressionEvidenceTests).Assembly.GetName().Name! },
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                Namespace = "PublicApiSurfaceSelectorTestFixtures.PublicSurface",
            },
            DeclaredApi = declared
                ? new List<string>
                {
                    $"class {typeName}",
                    $"ctor {typeName}()",
                    $"property {typeName}.Value: System.Int32",
                }
                : new List<string>(),
        };
    }

    private static ArchitectureAnalysisContext CreateContext()
    {
        ArchitectureDiscoveredProject[] projects = Enumerable.Range(0, DiscoveredProjectCount - 1)
            .Select(index => Project(index, AllowedPackage, "enable"))
            // Keep exactly 24 records while proving first-wins semantics for both assembly and
            // normalized-path maps: this late duplicate must never replace Project00.
            .Append(Project(0, ForbiddenPackage, "disable"))
            .ToArray();

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
            new[] { typeof(RepeatedWorkRegressionEvidenceTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
    }

    private static ArchitectureDiscoveredProject Project(int index, string packageId, string nullable)
    {
        string name = ProjectName(index);
        return new ArchitectureDiscoveredProject(
            $"src/{name}/{name}.csproj",
            name,
            _targetFrameworks,
            new[] { new ArchitectureDiscoveredPackageReference(packageId, "1.0.0") })
        {
            Properties = new Dictionary<string, ArchitectureDiscoveredProjectProperty>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nullable"] = new("Nullable", nullable, $"src/{name}/{name}.csproj"),
            },
        };
    }

    private static string ProjectName(int index) => $"Project{index:D2}";

    private static IReadOnlyList<string> CanonicalProjection(
        ArchitectureContractExecutionResult result,
        string mode)
    {
        return ArchitectureFindingMapper.Order(ArchitectureFindingMapper.FromViolations(result.Violations, mode))
            .Select(finding => $"{finding.ContractId}|{finding.Kind}|{finding.CanonicalIdentity}")
            .ToArray();
    }
}
