using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Testing;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

// Consumer-shaped deterministic evidence for #652/#653. Each family below uses its own fresh
// session so another family cannot seed the materialization counter and mask a local bypass.
[TestFixture]
public sealed class RepeatedWorkRegressionEvidenceTests
{
    private const int DiscoveredProjectCount = 24;
    private const int ContractFanOut = 16;
    private const string ForbiddenPackage = "Synthetic.Forbidden.Package";
    private const string ExpectedStrictProjectionChecksum = "925FD7BAB41B0F638A3C0ED73C3D09E50018FC1AB70E8C539E39FB8207581849";
    private const string ExpectedAuditProjectionChecksum = "9259819F5A173F5B054D99D3A0F7334DEF3154F1E30B5E954D0B46E688C161BE";
    private const int ExpectedStrictProjectionCount = 48;
    private const int ExpectedAuditProjectionCount = 3;
    private static readonly string[] _targetFrameworks = { "net10.0" };

    [Test]
    public void ConsumerShapedFanOut_MaterializesEveryCoveredPathExactlyOnce()
    {
        AssertPackageDependencyFanOut();
        AssertFrameworkReferenceFanOut();
        AssertAssemblyDependencyFanOut();
        AssertProjectMetadataFanOut();
        AssertPublicApiSurfaceFanOut();
    }

    [Test]
    public void ConsumerShapedFailures_MatchCheckedInCanonicalGolden()
    {
        ArchitectureAnalysisSession session = CreateSession(CreateDocument(failingMetadataContracts: true), out _);
        ArchitectureContractExecutor executor = new();
        ArchitectureContractHandlerRegistry registry = new();

        ArchitectureContractExecutionResult strict = executor.Execute(session, "strict", registry);
        ArchitectureContractExecutionResult audit = executor.Execute(session, "audit", registry);
        IReadOnlyList<string> strictProjection = CanonicalProjection(strict, "strict");
        IReadOnlyList<string> auditProjection = CanonicalProjection(audit, "audit");

        Assert.Multiple(() =>
        {
            Assert.That(strictProjection, Is.Not.Empty);
            Assert.That(auditProjection, Is.Not.Empty);
            Assert.That(strictProjection, Has.Count.EqualTo(ExpectedStrictProjectionCount));
            Assert.That(auditProjection, Has.Count.EqualTo(ExpectedAuditProjectionCount));
            Assert.That(CanonicalChecksum(strictProjection), Is.EqualTo(ExpectedStrictProjectionChecksum),
                $"Strict projection count={strictProjection.Count}, checksum={CanonicalChecksum(strictProjection)}");
            Assert.That(CanonicalChecksum(auditProjection), Is.EqualTo(ExpectedAuditProjectionChecksum),
                $"Audit projection count={auditProjection.Count}, checksum={CanonicalChecksum(auditProjection)}");
        });
    }

    [Test]
    public void TemporaryPolicy_PreservesTestingApiOutcomesAndCliExitSemantics()
    {
        using TemporaryPolicyFixture fixture = TemporaryPolicyFixture.Create();
        ArchitectureValidationResult strict = new ArchitectureValidationBuilder(fixture.PolicyPath).ValidateStrict();
        ArchitectureValidationResult audit = new ArchitectureValidationBuilder(fixture.PolicyPath).ValidateAudit();

        Assert.Multiple(() =>
        {
            Assert.That(strict.Mode, Is.EqualTo("strict"));
            Assert.That(strict.PreflightBlocked, Is.False);
            Assert.That(strict.Passed, Is.True);
            Assert.That(strict.Findings, Is.Empty);
            Assert.That(audit.Mode, Is.EqualTo("audit"));
            Assert.That(audit.PreflightBlocked, Is.False);
            Assert.That(audit.Passed, Is.False);
            Assert.That(audit.Findings, Has.Count.EqualTo(1));
            Assert.That(RunCli(fixture.PolicyPath, "strict"), Is.Zero);
            Assert.That(RunCli(fixture.PolicyPath, "audit"), Is.EqualTo(1));
        });
    }

    private static void AssertPackageDependencyFanOut()
    {
        ArchitectureAnalysisSession session = CreateSession(CreateDocument(), out ArchitectureAnalysisContext context);
        IReadOnlyList<ArchitecturePackageDependencyContract> contracts = session.Document.Contracts.StrictPackageDependency;

        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.Zero);
        Assert.That(session.CheckPackageDependencyContract(contracts[0]), Is.Not.Empty);
        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));

        foreach (ArchitecturePackageDependencyContract contract in contracts.Skip(1))
        {
            session.CheckPackageDependencyContract(contract);
        }

        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));
    }

    private static void AssertFrameworkReferenceFanOut()
    {
        ArchitectureAnalysisSession session = CreateSession(CreateDocument(), out ArchitectureAnalysisContext context);
        IReadOnlyList<ArchitectureFrameworkReferenceContract> contracts = session.Document.Contracts.StrictFrameworkDependency;

        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.Zero);
        session.CheckFrameworkDependencyContract(contracts[0]);
        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));

        foreach (ArchitectureFrameworkReferenceContract contract in contracts.Skip(1))
        {
            session.CheckFrameworkDependencyContract(contract);
        }

        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));
    }

    private static void AssertAssemblyDependencyFanOut()
    {
        ArchitectureAnalysisSession session = CreateSession(CreateDocument(), out ArchitectureAnalysisContext context);
        IReadOnlyList<ArchitectureAssemblyDependencyContract> contracts = session.Document.Contracts.StrictAssemblyDependency;

        Assert.That(context.ProfilingCounters.SessionAssemblyIndexMaterializations, Is.Zero);
        Assert.That(session.CheckAssemblyDependencyContract(contracts[0]), Is.Not.Empty);
        Assert.That(context.ProfilingCounters.SessionAssemblyIndexMaterializations, Is.EqualTo(1));

        foreach (ArchitectureAssemblyDependencyContract contract in contracts.Skip(1))
        {
            session.CheckAssemblyDependencyContract(contract);
        }

        Assert.That(context.ProfilingCounters.SessionAssemblyIndexMaterializations, Is.EqualTo(1));
    }

    private static void AssertProjectMetadataFanOut()
    {
        ArchitectureAnalysisSession session = CreateSession(CreateDocument(), out ArchitectureAnalysisContext context);
        IReadOnlyList<ArchitectureProjectMetadataContract> contracts = session.Document.Contracts.StrictProjectMetadata;

        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.Zero);
        Assert.That(session.CheckProjectMetadataContract(contracts[0]), Is.Empty);
        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));

        foreach (ArchitectureProjectMetadataContract contract in contracts.Skip(1))
        {
            session.CheckProjectMetadataContract(contract);
        }

        Assert.That(context.ProfilingCounters.SessionProjectMetadataIndexMaterializations, Is.EqualTo(1));
    }

    private static void AssertPublicApiSurfaceFanOut()
    {
        ArchitectureAnalysisSession session = CreateSession(CreateDocument(), out _);
        IReadOnlyList<ArchitecturePublicApiSurfaceContract> contracts = session.Document.Contracts.StrictPublicApiSurface;

        Assert.That(session.PublicApiSurfaceMaterializationCount, Is.Zero);
        Assert.That(session.CheckPublicApiSurfaceContract(contracts[0]), Is.Empty);
        Assert.That(session.PublicApiSurfaceMaterializationCount, Is.EqualTo(1));
        Assert.That(session.CheckPublicApiSurfaceContract(contracts[1]), Is.Empty);
        Assert.That(session.PublicApiSurfaceMaterializationCount, Is.EqualTo(1));
    }

    private static ArchitectureAnalysisSession CreateSession(
        ArchitectureContractDocument document,
        out ArchitectureAnalysisContext context)
    {
        context = CreateContext();
        return new ArchitectureAnalysisSession(context, document, null, false, null);
    }

    private static ArchitectureContractDocument CreateDocument(bool failingMetadataContracts = false)
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
                        Forbidden = new List<string> { "ArchLinterNet.Core" },
                    })
                    .ToList(),
                StrictProjectMetadata = Enumerable.Range(0, ContractFanOut)
                    .Select(index => new ArchitectureProjectMetadataContract
                    {
                        Name = $"metadata-{index:D2}",
                        Id = $"metadata-{index:D2}",
                        Projects = new List<string> { $"src/{ProjectName(index)}/{ProjectName(index)}.csproj" },
                        RequiredProperties = new Dictionary<string, string>
                        {
                            ["Nullable"] = failingMetadataContracts ? "disable" : "enable",
                        },
                    })
                    .ToList(),
                StrictPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract>
                {
                    PublicApiContract("strict-public-api-1", declared: true),
                    PublicApiContract("strict-public-api-2", declared: true),
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
        const string TypeName = "PublicApiSurfaceSelectorTestFixtures.PublicSurface.SelectedByNamespace";
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
                    $"class {TypeName}",
                    $"ctor {TypeName}()",
                    $"property {TypeName}.Value: System.Int32",
                }
                : new List<string>(),
        };
    }

    private static ArchitectureAnalysisContext CreateContext()
    {
        ArchitectureDiscoveredProject[] projects = Enumerable.Range(0, DiscoveredProjectCount)
            .Select(index => Project(index, ForbiddenPackage, "enable"))
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
        string mode) => ArchitectureFindingMapper.Order(ArchitectureFindingMapper.FromViolations(result.Violations, mode))
            .Select(finding => $"{finding.ContractId}|{finding.Kind}|{finding.CanonicalIdentity}")
            .ToArray();

    private static string CanonicalChecksum(IReadOnlyList<string> projection) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', projection))));

    private static int RunCli(string policyPath, string mode)
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        string cliDllPath = Path.Combine(root, "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");
        Assert.That(File.Exists(cliDllPath), Is.True, cliDllPath);

        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(cliDllPath);
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(policyPath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("json");

        using Process process = Process.Start(startInfo)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.That(stdout, Is.Not.Empty, stderr);
        return process.ExitCode;
    }

    private sealed class TemporaryPolicyFixture : IDisposable
    {
        private TemporaryPolicyFixture(string root, string policyPath)
        {
            Root = root;
            PolicyPath = policyPath;
        }

        public string Root { get; }

        public string PolicyPath { get; }

        public static TemporaryPolicyFixture Create()
        {
            string root = Path.Combine(Path.GetTempPath(), $"arch-linter-net-654-{Guid.NewGuid():N}");
            string projectDirectory = Path.Combine(root, "src", "Fixture");
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, "Fixture.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """);

            string policyPath = Path.Combine(root, "dependencies.arch.yml");
            File.WriteAllText(policyPath, """
                version: 1
                name: Repeated work outcome evidence
                layers: {}
                analysis:
                  target_assemblies: []
                  projects: [src/Fixture/Fixture.csproj]
                contracts:
                  strict_project_metadata:
                    - id: strict-nullable
                      name: strict-nullable
                      projects: [src/Fixture/Fixture.csproj]
                      required_properties:
                        Nullable: enable
                  audit_project_metadata:
                    - id: audit-nullable
                      name: audit-nullable
                      projects: [src/Fixture/Fixture.csproj]
                      required_properties:
                        Nullable: disable
                """);
            return new TemporaryPolicyFixture(root, policyPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
