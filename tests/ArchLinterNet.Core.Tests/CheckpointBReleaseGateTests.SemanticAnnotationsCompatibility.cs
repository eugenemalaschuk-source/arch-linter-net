using System.IO.Compression;
using System.Text;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    internal sealed partial class CandidatePackageFeed
    {
        private CheckpointScenarioResult AssertSemanticAnnotationsCustomOnlyConsumer()
        {
            string directory = Path.Combine(_root, "semantic-annotations-custom-only");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration><packageSources><clear />
                  <add key="candidate" value="{_feed}" />
                  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                </packageSources></configuration>
                """);
            File.WriteAllText(Path.Combine(directory, "CustomOnly.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
                  <ItemGroup><PackageReference Include="ArchLinterNet.Testing" Version="{_candidateVersion}" /></ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(directory, "Program.cs"), """
                using System.Text.Json;
                using ArchLinterNet.Testing;
                ArchitectureValidationResult result = new ArchitectureValidationBuilder(args[0]).ValidateStrict();
                Console.WriteLine(JsonSerializer.Serialize(new { passed = result.Passed, violations = result.Violations.Select(item => item.ContractId) }));
                """);
            File.WriteAllText(Path.Combine(directory, "CustomOnly.cs"), """
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
                public sealed class OwnedEntityAttribute(string context) : Attribute { public string Context { get; } = context; }
                namespace CustomOnly.Sales { [OwnedEntity("Sales")] public sealed class Order { public CustomOnly.Inventory.Stock? Stock { get; set; } } }
                namespace CustomOnly.Inventory { [OwnedEntity("Inventory")] public sealed class Stock { } }
                """);
            string policy = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(policy, """
                version: 1
                name: Custom-only compatibility consumer
                analysis:
                  target_assemblies: [CustomOnly]
                  assembly_search_paths: [bin/Debug/net10.0]
                classification:
                  attributes:
                    - attribute: OwnedEntityAttribute
                      role: Entity
                      metadata:
                        boundedContext: constructor[0]
                contracts:
                  strict_context_dependencies:
                    - id: custom-sales-no-inventory
                      name: Custom sales stay isolated
                      source: { role: Entity, metadata: { boundedContext: Sales } }
                      forbidden: [{ role: Entity, metadata: { boundedContext: Inventory } }]
                      reason: Custom-only mappings preserve existing behavior.
                """);
            CommandResult restore = RunIsolatedDotnet(directory, "restore", "CustomOnly.csproj", "--configfile", "NuGet.Config", "--no-cache");
            CommandResult build = RunIsolatedDotnet(directory, "build", "CustomOnly.csproj", "--no-restore", "--nologo", "--verbosity", "quiet");
            CommandResult cli = RunTool(directory, "--policy", policy, "--strict", "--format", "json");
            CommandResult testing = RunIsolatedDotnet(directory, "run", "--no-restore", "--", policy);
            string assets = File.ReadAllText(Path.Combine(directory, "obj", "project.assets.json"));
            Assert.Multiple(() =>
            {
                Assert.That(restore.ExitCode, Is.EqualTo(0), restore.CombinedOutput);
                Assert.That(build.ExitCode, Is.EqualTo(0), build.CombinedOutput);
                Assert.That(cli.ExitCode, Is.EqualTo(1), cli.CombinedOutput);
                Assert.That(cli.StandardOutput, Does.Contain("custom-sales-no-inventory"));
                Assert.That(testing.ExitCode, Is.EqualTo(0), testing.CombinedOutput);
                Assert.That(testing.StandardOutput, Does.Contain("custom-sales-no-inventory"));
                Assert.That(assets, Does.Not.Contain("ArchLinterNet.Annotations/"));
            });
            return Passed("semantic-annotations-custom-only-consumer");
        }

        private void AssertSemanticAnnotationPackageVersionSkew()
        {
            string root = Path.Combine(_root, "semantic-annotations-skew");
            string mirror = Path.Combine(root, "skew-feed");
            Directory.CreateDirectory(mirror);
            string packageName = Path.GetFileName(PackagePath("ArchLinterNet.Annotations"));
            string skewPackage = Path.Combine(mirror, packageName);
            using (ZipArchive sourcePackage = ZipFile.OpenRead(PackagePath("ArchLinterNet.Annotations")))
            using (FileStream output = File.Create(skewPackage))
            using (ZipArchive skewArchive = new(output, ZipArchiveMode.Create))
            {
                bool patchedMarker = false;
                foreach (ZipArchiveEntry entry in sourcePackage.Entries)
                {
                    ZipArchiveEntry copy = skewArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using Stream input = entry.Open();
                    using Stream destination = copy.Open();
                    if (entry.FullName.EndsWith("AnnotationCatalogIdentityAssemblyAttribute.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(input);
                        string markerSource = reader.ReadToEnd();
                        Assert.That(markerSource, Does.Contain(_candidateVersion));
                        byte[] changed = Encoding.UTF8.GetBytes(markerSource.Replace(_candidateVersion, "0.11.0", StringComparison.Ordinal));
                        destination.Write(changed);
                        patchedMarker = true;
                    }
                    else
                    {
                        input.CopyTo(destination);
                    }
                }
                Assert.That(patchedMarker, Is.True, "The packed annotation package must carry its injected catalog identity source.");
            }

            File.WriteAllText(Path.Combine(root, "NuGet.Config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration><packageSources><clear />
                  <add key="skew" value="{mirror}" />
                  <add key="candidate" value="{_feed}" />
                  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                </packageSources></configuration>
                """);
            File.WriteAllText(Path.Combine(root, "SkewConsumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
                  <ItemGroup><PackageReference Include="ArchLinterNet.Annotations" Version="{_candidateVersion}" /></ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(root, "SkewSubject.cs"), "using ArchLinterNet.Annotations; [Entity] public sealed class SkewSubject { }");
            string policy = Path.Combine(root, "dependencies.arch.yml");
            File.WriteAllText(policy, """
                version: 1
                name: Unsupported semantic annotation marker probe
                analysis:
                  target_assemblies: [SkewConsumer]
                  assembly_search_paths: [bin/Debug/net10.0]
                """);

            // Keep the NuGet cache outside the project tree. SDK default compile globs can
            // otherwise pick up source-only package content from this cache on macOS.
            string packageCache = Path.Combine(_root, "semantic-annotations-skew-packages");
            CommandResult restore = RunWithNugetPackages(root, packageCache,
                "restore", "SkewConsumer.csproj", "--configfile", "NuGet.Config", "--no-cache");
            CommandResult build = RunWithNugetPackages(root, packageCache,
                "build", "SkewConsumer.csproj", "--no-restore", "--nologo", "--verbosity", "quiet");
            CommandResult result = RunTool(root, "--policy", policy, "--strict", "--format", "json");
            Assert.Multiple(() =>
            {
                Assert.That(restore.ExitCode, Is.EqualTo(0), restore.CombinedOutput);
                Assert.That(build.ExitCode, Is.EqualTo(0), build.CombinedOutput);
                Assert.That(result.ExitCode, Is.EqualTo(0), result.CombinedOutput,
                    "An unsupported marker is an explicit fail-safe diagnostic; it does not silently classify as a canonical role.");
                Assert.That(result.StandardOutput, Does.Contain("UnsupportedAnnotationPackageVersion"));
                Assert.That(result.StandardOutput, Does.Contain("0.11.0"));
            });
        }

        private static CommandResult RunWithNugetPackages(string directory, string packageCache, params string[] arguments)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = directory,
            };
            startInfo.Environment["NUGET_PACKAGES"] = packageCache;
            startInfo.Environment["NUGET_HTTP_CACHE_PATH"] = Path.Combine(packageCache, "http-cache");
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            return Run(startInfo);
        }
    }
}
