using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private sealed partial class CandidatePackageFeed
    {
        /// <summary>
        /// Real ArchLinterNet.Testing cross-projection evidence for the v0.8 full-cycle shard,
        /// mirroring AssertPublicApiSurfaceSelectorTestingParity's established isolated-consumer
        /// pattern: an external consumer resolves the packaged ArchLinterNet.Testing from the
        /// candidate feed (not the source-compiled test-host assembly) and runs
        /// ArchitectureValidationBuilder against the same strict-mode policy the packed CLI just
        /// validated, printing each violation's canonical identity via the same
        /// ArchitectureViolationIdentityJson.Serialize wire projection the JSON/SARIF formatters use.
        /// </summary>
        public string[] RunTestingCanonicalIdentities(string policyPath)
        {
            string consumerDirectory = Path.Combine(_root, "v08-testing-projection-parity");
            WriteTestingProjectionParityHarness(consumerDirectory);

            CommandResult restore = RunIsolatedDotnet(consumerDirectory, "restore", "--configfile", "NuGet.Config", "--no-cache");
            Assert.That(restore.ExitCode, Is.EqualTo(0),
                $"v08-projection-parity (Testing consumer restore): {restore.CombinedOutput}");

            CommandResult testing = RunIsolatedDotnet(consumerDirectory, "run", "--no-restore", "--", policyPath);

            string jsonLine = testing.StandardOutput
                .Split('\n')
                .Select(line => line.Trim())
                .LastOrDefault(line => line.StartsWith('{'))
                ?? testing.StandardOutput;
            using JsonDocument document = JsonDocument.Parse(jsonLine);
            return document.RootElement.GetProperty("canonical_identities")
                .EnumerateArray()
                .Select(element => element.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id!)
                .ToArray();
        }

        private void WriteTestingProjectionParityHarness(string consumerDirectory)
        {
            Directory.CreateDirectory(consumerDirectory);
            File.WriteAllText(Path.Combine(consumerDirectory, "NuGet.Config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="candidate" value="{_feed}" />
                  </packageSources>
                </configuration>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "TestingProjectionParityConsumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="ArchLinterNet.Testing" Version="{_candidateVersion}" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "Program.cs"), """
                using System.Text.Json;
                using ArchLinterNet.Core.Model;
                using ArchLinterNet.Testing;

                string policyPath = args[0];

                ArchitectureValidationResult result = new ArchitectureValidationBuilder(policyPath)
                    .WithEnsureBuilt()
                    .ValidateStrict();

                string[] canonicalIdentities = result.Violations
                    .SelectMany(violation => violation.Identities.Count > 0
                        ? violation.Identities
                        : violation.Identity is { } identity ? [identity] : Array.Empty<ArchitectureViolationIdentity>())
                    .Select(ArchitectureViolationIdentityJson.Serialize)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                Console.WriteLine(JsonSerializer.Serialize(new { canonical_identities = canonicalIdentities }));
                return 0;
                """);
        }
    }
}
