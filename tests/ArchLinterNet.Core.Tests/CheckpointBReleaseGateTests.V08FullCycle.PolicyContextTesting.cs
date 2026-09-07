using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    internal sealed partial class CandidatePackageFeed
    {
        /// <summary>
        /// Proves the reported #788 boundary against the immutable candidate: the packed CLI emits
        /// both context files, then an isolated consumer restores ArchLinterNet.Testing and Core
        /// from this candidate feed and passes those files through EvaluateDebtGate.
        /// </summary>
        public CheckpointScenarioResult AssertExternalTestingPolicyContextConsumer(
            string consumerPolicyPath,
            string baselinePath,
            string baseContextPath,
            string currentContextPath)
        {
            string consumerDirectory = Path.Combine(_root, "testing-policy-context-consumer");
            WriteTestingPolicyContextHarness(consumerDirectory);

            CommandResult restore = RunIsolatedDotnet(consumerDirectory, "restore", "--configfile", "NuGet.Config", "--no-cache");
            CommandResult result = RunIsolatedDotnet(consumerDirectory, "run", "--no-restore", "--",
                consumerPolicyPath, baselinePath, baseContextPath, currentContextPath);
            string jsonLine = result.StandardOutput.Split('\n')
                .Select(line => line.Trim())
                .LastOrDefault(line => line.StartsWith('{'))
                ?? result.StandardOutput;

            using JsonDocument document = JsonDocument.Parse(jsonLine);
            JsonElement root = document.RootElement;
            JsonElement strictToAudit = root.GetProperty("strict_to_audit");
            JsonElement identical = root.GetProperty("identical");
            string testingAssemblyPath = root.GetProperty("testing_assembly").GetString()!;
            string coreAssemblyPath = root.GetProperty("core_assembly").GetString()!;
            string assets = File.ReadAllText(Path.Combine(consumerDirectory, "obj", "project.assets.json"));

            Assert.Multiple(() =>
            {
                Assert.That(restore.ExitCode, Is.EqualTo(0), restore.CombinedOutput);
                Assert.That(result.ExitCode, Is.EqualTo(0), result.CombinedOutput);
                Assert.That(strictToAudit.GetProperty("policy_weakening_requested").GetBoolean(), Is.True);
                Assert.That(strictToAudit.GetProperty("policy_weakening_has_errors").GetBoolean(), Is.True);
                Assert.That(strictToAudit.GetProperty("passed").GetBoolean(), Is.False);
                Assert.That(identical.GetProperty("policy_weakening_requested").GetBoolean(), Is.True);
                Assert.That(identical.GetProperty("policy_weakening_has_errors").GetBoolean(), Is.False);
                Assert.That(identical.GetProperty("passed").GetBoolean(), Is.True);
                Assert.That(File.Exists(testingAssemblyPath), Is.True, testingAssemblyPath);
                Assert.That(File.Exists(coreAssemblyPath), Is.True, coreAssemblyPath);
                Assert.That(testingAssemblyPath.Contains(_repositoryRoot, StringComparison.OrdinalIgnoreCase), Is.False);
                Assert.That(coreAssemblyPath.Contains(_repositoryRoot, StringComparison.OrdinalIgnoreCase), Is.False);
                Assert.That(Sha256(testingAssemblyPath), Is.EqualTo(PackageAssemblySha256("ArchLinterNet.Testing")));
                Assert.That(Sha256(coreAssemblyPath), Is.EqualTo(PackageAssemblySha256("ArchLinterNet.Core")));
                Assert.That(assets, Does.Contain($"ArchLinterNet.Testing/{_candidateVersion}"));
                Assert.That(assets, Does.Contain($"ArchLinterNet.Core/{_candidateVersion}"));
                Assert.That(CheckpointBReleaseGateNuGetAssets.ContainsPackageFolder(assets, Path.Combine(_root, "nuget-packages")), Is.True);
            });

            return Passed("external-testing-policy-context-consumer");
        }

        private void WriteTestingPolicyContextHarness(string consumerDirectory)
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
            File.WriteAllText(Path.Combine(consumerDirectory, "TestingPolicyContextConsumer.csproj"), $"""
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
                using ArchLinterNet.Core.Validation;
                using ArchLinterNet.Testing;

                string policyPath = args[0];
                string baselinePath = args[1];
                string baseContextPath = args[2];
                string currentContextPath = args[3];

                ArchitectureDebtGateOutcome strictToAudit = Evaluate(
                    policyPath, baselinePath, baseContextPath, currentContextPath);
                ArchitectureDebtGateOutcome identical = Evaluate(
                    policyPath, baselinePath, currentContextPath, currentContextPath);

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    testing_assembly = typeof(ArchitectureValidationBuilder).Assembly.Location,
                    core_assembly = typeof(ArchitectureDebtGateOutcome).Assembly.Location,
                    strict_to_audit = new
                    {
                        policy_weakening_requested = strictToAudit.PolicyWeakeningRequested,
                        policy_weakening_has_errors = strictToAudit.PolicyWeakening?.HasErrors == true,
                        passed = strictToAudit.Passed,
                    },
                    identical = new
                    {
                        policy_weakening_requested = identical.PolicyWeakeningRequested,
                        policy_weakening_has_errors = identical.PolicyWeakening?.HasErrors == true,
                        passed = identical.Passed,
                    },
                }));

                static ArchitectureDebtGateOutcome Evaluate(
                    string policyPath,
                    string baselinePath,
                    string baseContextPath,
                    string currentContextPath) => new ArchitectureValidationBuilder(policyPath)
                    .WithEnsureBuilt()
                    .WithContracts("modules-never-reference-the-host")
                    .WithBaseline(baselinePath)
                    .WithPolicyWeakeningContexts(baseContextPath, currentContextPath)
                    .EvaluateDebtGate("strict");
                """);
        }

        private string PackageAssemblySha256(string packageId)
        {
            using ZipArchive package = ZipFile.OpenRead(PackagePath(packageId));
            ZipArchiveEntry entry = package.Entries.Single(item =>
                item.FullName.EndsWith($"/{packageId}.dll", StringComparison.OrdinalIgnoreCase));
            using Stream assembly = entry.Open();
            return Convert.ToHexStringLower(SHA256.HashData(assembly));
        }
    }
}
