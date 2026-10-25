using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    internal sealed partial class CandidatePackageFeed
    {
        private void AssertNoRepositoryRootOutsideCandidateFeed(string content, string description)
        {
            string sanitized = content;
            foreach (string feedPath in PathTextVariants(_feed))
            {
                sanitized = sanitized.Replace(feedPath, string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            foreach (string repositoryPath in PathTextVariants(_repositoryRoot))
            {
                Assert.That(sanitized, Does.Not.Contain(repositoryPath), description);
            }
        }

        private static IEnumerable<string> PathTextVariants(string path)
        {
            string[] variants = [path, path.Replace('\\', '/'), path.Replace('/', '\\')];
            foreach (string variant in variants.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return variant;
                string jsonEscaped = JsonSerializer.Serialize(variant);
                yield return jsonEscaped[1..^1];
            }
        }

        /// <summary>
        /// Proves that freshly packed semantic annotations are consumed from an external SDK
        /// project alongside the candidate Testing package and installed CLI. The fixture is
        /// deliberately synthetic and exercises roles, bounded-context metadata, custom mapping,
        /// role conflicts, coverage, and all three CLI output projections.
        /// </summary>
        public CheckpointScenarioResult AssertSemanticAnnotationsPackedConsumer()
        {
            string consumerDirectory = Path.Combine(_root, "semantic-annotations-consumer");
            Directory.CreateDirectory(consumerDirectory);
            File.WriteAllText(Path.Combine(consumerDirectory, "NuGet.Config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="candidate" value="{_feed}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                  </packageSources>
                </configuration>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "SemanticConsumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="ArchLinterNet.Testing" Version="{_candidateVersion}" />
                    <Compile Include="Program.cs" />
                    <Compile Include="SemanticConsumer.cs" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "Directory.Build.props"), $"""
                <Project>
                  <ItemGroup>
                    <PackageReference Include="ArchLinterNet.Annotations" Version="{_candidateVersion}" />
                  </ItemGroup>
                </Project>
                """);
            string[] shapeProjects = WriteSemanticShapeProjects(consumerDirectory);
            string[] assemblySearchPaths = new[] { "bin/Debug/net10.0" }
                .Concat(shapeProjects.Select(projectPath =>
                    Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", "Debug", "net10.0")
                        .Replace('\\', '/')))
                .ToArray();
            string assemblySearchPathYaml = string.Join(Environment.NewLine,
                assemblySearchPaths.Select(path => $"    - {path}"));
            File.WriteAllText(Path.Combine(consumerDirectory, "PublicLibrary.slnx"), """
                <Solution>
                  <Project Path="Public/Library/Public.Library.csproj" />
                </Solution>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "SemanticConsumer.cs"), """
                using ArchLinterNet.Annotations;

                [assembly: Domain("Commerce")]

                namespace SemanticConsumer.Domain.Sales
                {
                    [Entity(BoundedContext = "Sales", Module = "Orders")]
                    public sealed class Order
                    {
                        public SemanticConsumer.Domain.Inventory.Stock? Stock { get; set; }
                        public MixedOrder? Mixed { get; set; }
                    }

                    [Entity(BoundedContext = "Sales")]
                    [CustomEntity("Sales")]
                    public sealed class MixedOrder { public SemanticConsumer.Domain.Inventory.Stock? Stock { get; set; } }

                    [Entity]
                    [AggregateRoot]
                    public sealed class ConflictingSubject { }

                    [AggregateRoot]
                    [Entity]
                    public sealed class ReversedConflictingSubject { }

                    [FutureUnsupportedRole]
                    public sealed class UnsupportedMarkerSubject { }

                    [BoundedContext("Sales")]
                    public sealed class ContextOnlySubject { }
                }

                namespace SemanticConsumer.Domain.Inventory
                {
                    [Entity(BoundedContext = "Inventory")]
                    public sealed class Stock { }
                }

                namespace SemanticConsumer.Application
                {
                    [Command] public sealed class PlaceOrder { }
                    [CommandHandler] public sealed class PlaceOrderHandler { }
                    [Query] public sealed class FindOrder { }
                    [QueryHandler] public sealed class FindOrderHandler { }
                }

                namespace SemanticConsumer.Api
                {
                    [Endpoint] public sealed class OrdersEndpoint { }
                    [ApiContract] public sealed class OrdersContract { }
                }

                namespace SemanticConsumer.Infrastructure
                {
                    [Adapter] public sealed class InventoryAdapter { }
                    [RepositoryImplementation] public sealed class OrderRepository { }
                }

                namespace SemanticConsumer.PublicLibrary
                {
                    [ReviewedApi] public interface IOrderClient { }
                    [ApiContract] public sealed class ApiContractRole { }
                }

                namespace SemanticConsumer.Unity
                {
                    public sealed class UnityBootstrap { }
                }

                [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
                public sealed class CustomEntityAttribute(string value) : Attribute
                {
                    public string Value { get; } = value;
                }

                [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
                public sealed class ReviewedApiAttribute : Attribute { }

                namespace ArchLinterNet.Annotations
                {
                    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
                    internal sealed class FutureUnsupportedRoleAttribute : Attribute { }
                }
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "Program.cs"), """
                using System.Text.Json;
                using System.Reflection;
                using ArchLinterNet.Testing;

                ArchitectureValidationResult result = new ArchitectureValidationBuilder(args[0])
                    .ValidateStrict();
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    result.Passed,
                    violations = result.Violations.Select(item => item.ContractId).ToArray(),
                    canonical_diagnostics = result.CanonicalAnnotationDiagnostics.Select(item => new
                    {
                        item.Code,
                        item.Message,
                        item.EvidenceSources
                    }).ToArray(),
                    coverage = result.CoverageSummaries.Select(item => new
                    {
                        item.ContractId,
                        item.Counts,
                        item.CoveredItems,
                        item.UncoveredItems
                    }).ToArray(),
                    testing_assembly = typeof(ArchitectureValidationBuilder).Assembly.Location,
                    annotation_exports = typeof(ArchitectureValidationBuilder).Assembly
                        .GetExportedTypes()
                        .Where(type => type.Namespace == "ArchLinterNet.Annotations")
                        .Select(type => type.FullName)
                        .ToArray(),
                    injected_annotation_exports = Assembly.GetEntryAssembly()!.GetExportedTypes()
                        .Where(type => type.Namespace == "ArchLinterNet.Annotations")
                        .Select(type => type.FullName)
                        .ToArray(),
                }));
                """);
            string policyPath = Path.Combine(consumerDirectory, "dependencies.arch.yml");
            File.WriteAllText(policyPath, $$"""
                version: 1
                name: Packed semantic annotations consumer
                layers:
                  modular-domain:
                    selector: { role: DomainLayer }
                  modular-application:
                    selector: { role: ApplicationLayer }
                  sales:
                    selector:
                      role: Entity
                      metadata:
                        domain: Commerce
                        boundedContext: Sales
                  inventory:
                    selector:
                      role: Entity
                      metadata:
                        domain: Commerce
                        boundedContext: Inventory
                analysis:
                  target_assemblies: [SemanticConsumer, PlainLibrary, Modular.Domain, Modular.Application, Ddd.Domain, Cqrs.Api, Infrastructure.Adapters, Public.Library, Unity.Client]
                  assembly_search_paths:
                {{assemblySearchPathYaml}}
                classification:
                  attributes:
                    - attribute: CustomEntityAttribute
                      role: Entity
                      metadata:
                        boundedContext: constructor[0]
                contracts:
                  strict_public_api_surface:
                    - id: reviewed-api-membership
                      name: Reviewed public API membership
                      assemblies: [Public.Library]
                      surface_selector:
                        has_attribute: Public.Library.ReviewedApiAttribute
                      api_snapshot: semantic-api.snapshot
                      api_comparison: exact
                      reason: The deliberately marked reusable API is reviewed independently of its semantic role.
                  strict_context_dependencies:
                    - id: sales-no-inventory
                      name: Sales entities must not reference Inventory entities
                      source:
                        role: Entity
                        metadata:
                          domain: Commerce
                          boundedContext: Sales
                      forbidden:
                        - role: Entity
                          metadata:
                            domain: Commerce
                            boundedContext: Inventory
                      reason: Synthetic bounded contexts remain isolated.
                  strict_assembly_dependency:
                    - id: application-assembly-must-not-reference-domain-assembly
                      name: Application must not take a direct Domain assembly reference
                      source: Modular.Application
                      forbidden: [Modular.Domain]
                      reason: Assembly-level semantic layer roles stay visible through role selectors while direct assembly coupling remains separately governed.
                  strict_layers:
                    - id: modular-semantic-layers-exist
                      name: Modular semantic layers exist
                      layers: [modular-application, modular-domain]
                      reason: The assembly annotations must feed the existing selector-backed layer model.
                  strict_coverage:
                    - id: semantic-entity-coverage
                      name: Semantic entity coverage
                      scope: semantic_role
                      roots:
                        - namespace: SemanticConsumer.Domain
                        - namespace: PlainLibrary
                        - namespace: Modular
                        - namespace: Ddd
                        - namespace: Cqrs
                        - namespace: Infrastructure
                        - namespace: Public.Library
                        - namespace: Unity.Client
                      reason: Synthetic domain entities are explicitly inventoried.
                """);

            string publicApiPolicyPath = Path.Combine(consumerDirectory, "public-api.arch.yml");
            File.WriteAllText(publicApiPolicyPath, """
                version: 1
                name: Packed semantic annotations public API capture
                analysis:
                  solution: PublicLibrary.slnx
                  configuration: Debug
                  target_framework: net10.0
                  target_assemblies: [Public.Library]
                contracts:
                  strict_public_api_surface:
                    - id: reviewed-api-membership
                      name: Reviewed public API membership
                      assemblies: [Public.Library]
                      surface_selector:
                        has_attribute: Public.Library.ReviewedApiAttribute
                      api_snapshot: semantic-api.snapshot
                      api_comparison: exact
                      reason: The deliberately marked reusable API is reviewed independently of its semantic role.
                """);

            List<CommandResult> restoreBuild = new();
            foreach (string project in Directory.GetFiles(consumerDirectory, "*.csproj", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                restoreBuild.Add(RunIsolatedDotnet(consumerDirectory, "restore", project,
                    "--configfile", "NuGet.Config", "--no-cache"));
                if (!string.Equals(Path.GetFileName(project), "Public.Library.csproj", StringComparison.Ordinal))
                {
                    restoreBuild.Add(RunIsolatedDotnet(consumerDirectory, "build", project,
                        "--no-restore", "--nologo", "--verbosity", "quiet"));
                }
            }
            foreach (CommandResult command in restoreBuild)
            {
                Assert.That(command.ExitCode, Is.EqualTo(0), command.CombinedOutput);
            }

            CommandResult apiCapture = RunTool(consumerDirectory, "public-api", "capture", "--policy", publicApiPolicyPath,
                "--contract", "reviewed-api-membership", "--output", "semantic-api.snapshot", "--format", "json", "--ensure-built");
            Assert.That(apiCapture.ExitCode, Is.EqualTo(0), apiCapture.CombinedOutput);
            string apiSnapshot = File.ReadAllText(Path.Combine(consumerDirectory, "semantic-api.snapshot"));
            string outputDirectory = Path.Combine(consumerDirectory, "bin", "Debug", "net10.0");
            string assetsPath = Path.Combine(consumerDirectory, "obj", "project.assets.json");
            string depsPath = Path.Combine(outputDirectory, "SemanticConsumer.deps.json");
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(PackagePath("ArchLinterNet.Annotations")), Is.True);
                Assert.That(apiSnapshot, Does.Contain("IOrderClient"));
                Assert.That(apiSnapshot, Does.Not.Contain("ApiContractRole"));
                Assert.That(apiSnapshot, Does.Not.Contain("EntityAttribute"));
                Assert.That(File.ReadAllText(assetsPath), Does.Contain($"ArchLinterNet.Annotations/{_candidateVersion}"));
                Assert.That(File.ReadAllText(assetsPath), Does.Contain($"ArchLinterNet.Testing/{_candidateVersion}"));
                Assert.That(File.ReadAllText(assetsPath), Does.Contain($"ArchLinterNet.Core/{_candidateVersion}"));
                AssertNoRepositoryRootOutsideCandidateFeed(File.ReadAllText(assetsPath), assetsPath);
                foreach (string projectPath in Directory.GetFiles(consumerDirectory, "*.csproj", SearchOption.AllDirectories))
                {
                    Assert.That(File.ReadAllText(projectPath), Does.Not.Contain(_repositoryRoot), projectPath);
                    XDocument project = XDocument.Load(projectPath);
                    foreach (XElement reference in project.Descendants("ProjectReference"))
                    {
                        string include = reference.Attribute("Include")?.Value ?? string.Empty;
                        string resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, include));
                        Assert.That(resolved.StartsWith(consumerDirectory, StringComparison.OrdinalIgnoreCase), Is.True, projectPath);
                        Assert.That(File.Exists(resolved), Is.True, resolved);
                    }
                }
                foreach (string projectAssets in Directory.GetFiles(consumerDirectory, "project.assets.json", SearchOption.AllDirectories))
                {
                    string content = File.ReadAllText(projectAssets);
                    Assert.That(content, Does.Contain($"ArchLinterNet.Annotations/{_candidateVersion}"), projectAssets);
                    AssertNoRepositoryRootOutsideCandidateFeed(content, projectAssets);
                }
                Assembly publicLibrary = Assembly.Load(File.ReadAllBytes(Path.GetFullPath(Path.Combine(consumerDirectory,
                    "Public", "Library", "bin", "Debug", "net10.0", "Public.Library.dll"))));
                Assert.That(publicLibrary.GetExportedTypes().Any(type => type.Name == "ApiContractRole"), Is.True,
                    "The semantic ApiContract role remains CLR-public.");
                Assert.That(Directory.GetFiles(consumerDirectory, "ArchLinterNet.Annotations.dll", SearchOption.AllDirectories), Is.Empty);
                foreach (string dependencyManifest in Directory.GetFiles(consumerDirectory, "*.deps.json", SearchOption.AllDirectories))
                {
                    Assert.That(File.ReadAllText(dependencyManifest), Does.Not.Contain("ArchLinterNet.Annotations/"));
                }
            });

            CommandResult human = RunTool(consumerDirectory, "--policy", policyPath, "--strict");
            CommandResult json = RunTool(consumerDirectory, "--policy", policyPath, "--strict", "--format", "json");
            CommandResult sarif = RunTool(consumerDirectory, "--policy", policyPath, "--strict", "--format", "sarif");
            CommandResult testing = RunIsolatedDotnet(consumerDirectory, "run", "--no-restore", "--", policyPath);
            string validationJsonPath = Path.Combine(consumerDirectory, "validation.json");
            File.WriteAllText(validationJsonPath, json.StandardOutput);
            string coverageMarkdownPath = Path.Combine(consumerDirectory, "coverage.md");
            CommandResult coverageReport = RunTool(consumerDirectory, "coverage", "report", "--input", validationJsonPath,
                "--repo-root", consumerDirectory, "--output", coverageMarkdownPath);
            string noAuthorityPolicy = Path.Combine(consumerDirectory, "no-implicit-authority.yml");
            File.WriteAllText(noAuthorityPolicy, """
                version: 1
                name: Annotation presence grants no policy authority
                analysis:
                  target_assemblies: [PlainLibrary]
                  assembly_search_paths: [PlainLibrary/bin/Debug/net10.0]
                """);
            CommandResult noAuthority = RunTool(consumerDirectory, "--policy", noAuthorityPolicy, "--strict", "--format", "json");
            CommandResult noAuthorityTesting = RunIsolatedDotnet(consumerDirectory, "run", "--no-restore", "--", noAuthorityPolicy);
            CommandResult explain = RunTool(consumerDirectory, "explain", "--policy", policyPath,
                "--source", "Modular.Application", "--target", "Modular.Domain");

            using JsonDocument testingDocument = JsonDocument.Parse(testing.StandardOutput);
            using JsonDocument cliDocument = JsonDocument.Parse(json.StandardOutput);
            string testingAssembly = testingDocument.RootElement.GetProperty("testing_assembly").GetString()!;
            string testingAssemblyHash = Sha256(testingAssembly);
            JsonElement[] roleConflicts = testingDocument.RootElement.GetProperty("canonical_diagnostics")
                .EnumerateArray()
                .Where(item => item.GetProperty("Code").GetString() == "CanonicalRoleConflict")
                .ToArray();
            string[][] conflictEvidence = roleConflicts.Select(item => item.GetProperty("EvidenceSources")
                .EnumerateArray().Select(source => source.GetString()!).ToArray()).ToArray();
            int salesContractViolations = testingDocument.RootElement.GetProperty("violations")
                .EnumerateArray().Count(item => item.GetString() == "sales-no-inventory");
            string[] testingContractIds = testingDocument.RootElement.GetProperty("violations")
                .EnumerateArray().Select(item => item.GetString()!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            JsonElement semanticCoverage = testingDocument.RootElement.GetProperty("coverage").EnumerateArray()
                .Single(item => item.GetProperty("ContractId").GetString() == "semantic-entity-coverage");
            bool contextOnlyRemainsUnclassified = semanticCoverage.GetProperty("UncoveredItems").EnumerateArray()
                .Any(item => item.GetProperty("Item").GetString() == "SemanticConsumer.Domain.Sales.ContextOnlySubject"
                    && item.GetProperty("Evidence").GetString() == "unclassified semantic fact");
            bool plainLibraryEntityIsUncovered = semanticCoverage.GetProperty("UncoveredItems").EnumerateArray()
                .Any(item => item.GetProperty("Item").GetString() == "PlainLibrary.PlainRecord"
                    && item.GetProperty("Evidence").GetString() == "role=Entity");
            JsonElement cliViolations = FindJsonProperty(cliDocument.RootElement, "violations");
            JsonElement cliCanonicalDiagnostics = FindJsonProperty(cliDocument.RootElement, "canonical_annotation_diagnostics");
            string[] cliContractIds = CollectJsonStrings(cliViolations, "contract_id", "contractId")
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string[] testingDiagnosticCodes = testingDocument.RootElement.GetProperty("canonical_diagnostics")
                .EnumerateArray().Select(item => item.GetProperty("Code").GetString()!)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string[] cliDiagnosticCodes = CollectJsonStrings(cliCanonicalDiagnostics, "code")
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            using ZipArchive testingPackage = ZipFile.OpenRead(PackagePath("ArchLinterNet.Testing"));
            ZipArchiveEntry packedTestingAssembly = testingPackage.Entries.Single(entry =>
                entry.FullName.EndsWith("/ArchLinterNet.Testing.dll", StringComparison.OrdinalIgnoreCase));
            using Stream packedAssembly = packedTestingAssembly.Open();
            string packedTestingHash = Convert.ToHexStringLower(SHA256.HashData(packedAssembly));

            Assert.Multiple(() =>
            {
                Assert.That(human.ExitCode, Is.EqualTo(1), human.CombinedOutput);
                Assert.That(human.StandardOutput, Does.Contain("sales-no-inventory"));
                Assert.That(human.StandardOutput, Does.Contain("application-assembly-must-not-reference-domain-assembly"));
                Assert.That(human.StandardOutput, Does.Contain("CanonicalRoleConflict"));
                Assert.That(json.ExitCode, Is.EqualTo(1), json.CombinedOutput);
                Assert.That(json.StandardOutput, Does.Contain("sales-no-inventory"));
                Assert.That(cliContractIds, Is.EqualTo(testingContractIds), "Packed CLI and Testing must project the same contract violations.");
                Assert.That(cliDiagnosticCodes, Is.EqualTo(testingDiagnosticCodes), "Packed CLI and Testing must project the same canonical diagnostic codes.");
                Assert.That(sarif.ExitCode, Is.EqualTo(1), sarif.CombinedOutput);
                Assert.That(sarif.StandardOutput, Does.Contain("sales-no-inventory"));
                Assert.That(sarif.StandardOutput, Does.Contain("application-assembly-must-not-reference-domain-assembly"));
                Assert.That(sarif.StandardOutput, Does.Contain("CanonicalRoleConflict"));
                Assert.That(coverageReport.ExitCode, Is.EqualTo(0), coverageReport.CombinedOutput);
                Assert.That(File.ReadAllText(coverageMarkdownPath), Does.Contain("semantic-entity-coverage"));
                Assert.That(File.ReadAllText(coverageMarkdownPath), Does.Contain("SemanticConsumer.Domain.Sales.Order"));
                Assert.That(testing.ExitCode, Is.EqualTo(0), testing.CombinedOutput);
                Assert.That(testing.StandardOutput, Does.Contain("sales-no-inventory"));
                Assert.That(testing.StandardOutput, Does.Contain("application-assembly-must-not-reference-domain-assembly"));
                Assert.That(testing.StandardOutput, Does.Contain("semantic-entity-coverage"));
                Assert.That(testing.StandardOutput, Does.Contain("CanonicalRoleConflict"));
                Assert.That(testing.StandardOutput, Does.Contain("UnknownReservedAnnotation"));
                Assert.That(roleConflicts, Has.Length.EqualTo(2), "Reversing conflicting attribute source order must report the same deterministic pair of conflicts.");
                Assert.That(conflictEvidence[0], Is.EqualTo(conflictEvidence[1]));
                Assert.That(conflictEvidence[0], Is.EqualTo(new[]
                {
                    "ArchLinterNet.Annotations.AggregateRootAttribute",
                    "ArchLinterNet.Annotations.EntityAttribute"
                }));
                Assert.That(salesContractViolations, Is.GreaterThanOrEqualTo(2),
                    "Canonical and equivalent custom mappings must both feed the existing context selector contract.");
                Assert.That(plainLibraryEntityIsUncovered, Is.True,
                    "A classified role remains visible as a role fact even when no policy governs its coverage.");
                AssertSemanticRoleFact(semanticCoverage, "PlainLibrary.PlainRecord", "Entity");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Domain.Sales.Order", "Entity",
                    "domain=Commerce", "boundedContext=Sales", "module=Orders");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Domain.Inventory.Stock", "Entity",
                    "domain=Commerce", "boundedContext=Inventory");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Application.PlaceOrder", "Command");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Application.PlaceOrderHandler", "CommandHandler");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Application.FindOrder", "Query");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Application.FindOrderHandler", "QueryHandler");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Api.OrdersEndpoint", "Endpoint");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Api.OrdersContract", "ApiContract");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Infrastructure.InventoryAdapter", "Adapter");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Infrastructure.OrderRepository", "RepositoryImplementation");
                AssertSemanticRoleFact(semanticCoverage, "SemanticConsumer.Domain.Sales.MixedOrder", "Entity",
                    "domain=Commerce", "boundedContext=Sales");
                AssertSemanticRoleFact(semanticCoverage, "Modular.Domain.Order", "DomainLayer");
                AssertSemanticRoleFact(semanticCoverage, "Modular.Application.OrderService", "ApplicationLayer");
                AssertSemanticRoleFact(semanticCoverage, "Ddd.Domain.Order", "AggregateRoot", "boundedContext=Sales");
                AssertSemanticRoleFact(semanticCoverage, "Cqrs.Api.PlaceOrder", "Command");
                AssertSemanticRoleFact(semanticCoverage, "Cqrs.Api.PlaceOrderHandler", "CommandHandler");
                AssertSemanticRoleFact(semanticCoverage, "Cqrs.Api.FindOrder", "Query");
                AssertSemanticRoleFact(semanticCoverage, "Cqrs.Api.OrdersEndpoint", "Endpoint");
                AssertSemanticRoleFact(semanticCoverage, "Infrastructure.Adapters.InventoryAdapter", "Adapter");
                AssertSemanticRoleFact(semanticCoverage, "Infrastructure.Adapters.OrderRepository", "RepositoryImplementation");
                AssertSemanticRoleFact(semanticCoverage, "Public.Library.ApiContractRole", "ApiContract");
                AssertSemanticRoleFact(semanticCoverage, "Unity.Client.Bootstrap", "UnityRuntime");
                Assert.That(testing.StandardOutput, Does.Contain("injected_annotation_exports\":[]"));
                Assert.That(contextOnlyRemainsUnclassified, Is.True,
                    "BoundedContext metadata alone must not infer a semantic role.");
                Assert.That(noAuthority.ExitCode, Is.EqualTo(0), noAuthority.CombinedOutput);
                Assert.That(noAuthorityTesting.ExitCode, Is.EqualTo(0), noAuthorityTesting.CombinedOutput);
                Assert.That(noAuthorityTesting.StandardOutput, Does.Contain("\"Passed\":true"));
                Assert.That(explain.ExitCode, Is.EqualTo(0), explain.CombinedOutput);
                Assert.That(explain.CombinedOutput, Does.Contain("Modular.Application"));
                Assert.That(explain.CombinedOutput, Does.Contain("Modular.Domain"));
                Assert.That(testingAssembly.Contains(_repositoryRoot, StringComparison.OrdinalIgnoreCase), Is.False);
                Assert.That(testingAssemblyHash, Is.EqualTo(packedTestingHash));
                Assert.That(testing.StandardOutput, Does.Not.Contain("successful"));
            });

            CheckpointScenarioResult customOnly = AssertSemanticAnnotationsCustomOnlyConsumer();
            Assert.That(customOnly.Result, Is.EqualTo("passed"));
            AssertSemanticAnnotationPackageVersionSkew();

            return Passed("semantic-annotations-packed-consumer");
        }


    }
}
