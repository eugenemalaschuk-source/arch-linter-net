using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private sealed partial class CandidatePackageFeed
    {
        /// <summary>
        /// Item 13 — the CLI and the packaged <c>ArchLinterNet.Testing</c> API must resolve the same
        /// effective selected surface and normalized findings. A temporary member added to the
        /// selected <c>Receipt</c> type is observed identically by a direct CLI diff and by an
        /// external consumer that resolves the candidate <c>ArchLinterNet.Testing</c> package from
        /// the isolated feed and runs <c>WithContracts("marker-selected-api")</c>.
        /// </summary>
        public CheckpointScenarioResult AssertPublicApiSurfaceSelectorTestingParity(AdoptionAcceptanceFixture fixture)
        {
            string consumerDirectory = Path.Combine(_root, "public-api-surface-selector-parity");
            WriteSurfaceSelectorParityHarness(consumerDirectory);

            CommandResult restore = RunIsolatedDotnet(consumerDirectory, "restore", "--configfile", "NuGet.Config", "--no-cache");
            Assert.That(restore.ExitCode, Is.EqualTo(0), restore.CombinedOutput);

            string receiptPath = Path.Combine(fixture.Root, "Domain", "Receipt.cs");
            string synced = File.ReadAllText(receiptPath);
            string mutated = synced.Replace(
                "public long Changed(int value) => value;",
                "public long Changed(int value) => value;" + Environment.NewLine + Environment.NewLine
                + "    public string Extra() => \"extra\";");

            CommandResult cli;
            CommandResult testing;
            try
            {
                File.WriteAllText(receiptPath, mutated);
                fixture.Build(configuration: "Release", targetFramework: "net10.0");

                cli = RunTool(fixture.Root,
                    "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
                    "--snapshot", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");
                testing = RunIsolatedDotnet(consumerDirectory, "run", "--no-restore", "--",
                    fixture.PolicyPath, "marker-selected-api");
            }
            finally
            {
                File.WriteAllText(receiptPath, synced);
                fixture.Build(configuration: "Release", targetFramework: "net10.0");
                RunTool(fixture.Root,
                    "public-api", "update", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
                    "--snapshot", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");
            }

            using JsonDocument cliDocument = JsonDocument.Parse(cli.StandardOutput);
            (string DeltaKind, string Signature)[] cliDeltas = cliDocument.RootElement.GetProperty("violations")
                .EnumerateArray()
                .Where(violation => violation.GetProperty("source").GetString() ==
                    "Synthetic.ApiSurfaceSelector.Domain.Receipt")
                .Select(violation => (
                    violation.GetProperty("api_delta_kind").GetString() ?? string.Empty,
                    violation.GetProperty("undeclared_api_signature").GetString() ?? string.Empty))
                .OrderBy(delta => delta.Item2, StringComparer.Ordinal)
                .ToArray();

            string testingJsonLine = testing.StandardOutput
                .Split('\n')
                .Select(line => line.Trim())
                .LastOrDefault(line => line.StartsWith('{'))
                ?? testing.StandardOutput;
            using JsonDocument testingDocument = JsonDocument.Parse(testingJsonLine);
            (string DeltaKind, string Signature)[] testingDeltas = testingDocument.RootElement
                .GetProperty("violations").EnumerateArray()
                .Where(violation => violation.GetProperty("source_type").GetString() ==
                    "Synthetic.ApiSurfaceSelector.Domain.Receipt")
                .Select(violation => (
                    violation.GetProperty("api_delta_kind").GetString() ?? string.Empty,
                    violation.GetProperty("undeclared_api_signature").GetString() ?? string.Empty))
                .OrderBy(delta => delta.Item2, StringComparer.Ordinal)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(cli.ExitCode, Is.EqualTo(1), cli.CombinedOutput);
                Assert.That(testingDocument.RootElement.GetProperty("passed").GetBoolean(), Is.False,
                    testing.CombinedOutput);
                Assert.That(cliDeltas, Is.Not.Empty);
                Assert.That(cliDeltas.Select(d => d.DeltaKind), Does.Contain("added"));
                Assert.That(testingDeltas, Is.EquivalentTo(cliDeltas),
                    $"The CLI and the packaged Testing API must resolve the same effective selected "
                    + $"surface and normalized findings.{Environment.NewLine}cli: {cli.CombinedOutput}"
                    + $"{Environment.NewLine}testing: {testing.CombinedOutput}");
            });
            return Passed("public-api-surface-selector-testing-parity");
        }

        private void WriteSurfaceSelectorParityHarness(string consumerDirectory)
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
            File.WriteAllText(Path.Combine(consumerDirectory, "SurfaceSelectorParityConsumer.csproj"), $"""
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
                string contractId = args[1];

                ArchitectureValidationResult result = new ArchitectureValidationBuilder(policyPath)
                    .WithContracts(contractId)
                    .WithEnsureBuilt()
                    .ValidateStrict();

                var payload = new
                {
                    passed = result.Passed,
                    violations = result.Violations.Select(violation => new
                    {
                        source_type = violation.SourceType,
                        api_delta_kind = (violation.Payload as PublicApiSurfacePayload)?.ApiDeltaKind,
                        undeclared_api_signature = (violation.Payload as PublicApiSurfacePayload)?.UndeclaredApiSignature,
                    }),
                };
                Console.WriteLine(JsonSerializer.Serialize(payload));
                return result.Passed ? 0 : 1;
                """);
        }
    }
}
