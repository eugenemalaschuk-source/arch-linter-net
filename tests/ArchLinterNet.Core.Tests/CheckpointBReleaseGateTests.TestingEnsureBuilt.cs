using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private sealed partial class CandidatePackageFeed
    {
        /// <summary>
        /// F2 through the packaged <c>ArchLinterNet.Testing</c> surface. Issue #436 was reproduced
        /// in Buildalyzer's post-build project evaluation, which the Testing API drives as well, so
        /// the CLI check alone cannot authorize the finding. An external consumer resolves the
        /// candidate package from the isolated feed and runs two back-to-back
        /// <c>WithEnsureBuilt()</c> validations in one process, then proves that the selected
        /// primary outputs it will hand to `dotnet test --no-build` are still byte-identical.
        /// </summary>
        public CheckpointScenarioResult AssertRepeatedTestingEnsureBuilt()
        {
            string consumerDirectory = Path.Combine(_root, "testing-ensure-built");
            string targetDirectory = Path.Combine(consumerDirectory, "target");
            Directory.CreateDirectory(targetDirectory);
            WriteEnsureBuiltTarget(targetDirectory);
            WriteEnsureBuiltHarness(consumerDirectory);

            CommandResult restore = RunIsolatedDotnet(consumerDirectory, "restore", "--configfile", "NuGet.Config", "--no-cache");
            Assert.That(restore.ExitCode, Is.EqualTo(0), restore.CombinedOutput);
            CommandResult result = RunIsolatedDotnet(consumerDirectory, "run", "--no-restore");

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0),
                    $"Two consecutive packaged WithEnsureBuilt() validations must succeed without a "
                    + $"rebuild and must preserve the selected primary outputs.{Environment.NewLine}"
                    + result.CombinedOutput);
                Assert.That(result.StandardOutput, Does.Contain("first-validation-completed"));
                Assert.That(result.StandardOutput, Does.Contain("second-validation-completed"));
                Assert.That(result.StandardOutput, Does.Contain("primary-outputs-preserved"));
                Assert.That(result.StandardOutput, Does.Not.Contain(_repositoryRoot));
            });
            return Passed("packaged-testing-ensure-built");
        }

        private static void WriteEnsureBuiltTarget(string targetDirectory)
        {
            File.WriteAllText(Path.Combine(targetDirectory, "SyntheticEnsureBuiltTarget.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <DebugType>portable</DebugType>
                    <AssemblyName>SyntheticEnsureBuiltTarget</AssemblyName>
                    <RootNamespace>SyntheticEnsureBuiltTarget</RootNamespace>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(targetDirectory, "Domain.cs"), """
                namespace SyntheticEnsureBuiltTarget.Domain;

                public sealed class Order
                {
                    public string Reference { get; init; } = string.Empty;
                }
                """);
            File.WriteAllText(Path.Combine(targetDirectory, "dependencies.arch.yml"), """
                version: 1
                name: Synthetic packaged Testing ensure-built consumer
                layers:
                  domain:
                    namespace: SyntheticEnsureBuiltTarget.Domain
                contracts:
                  strict:
                    - id: domain-is-self-contained
                      name: domain-is-self-contained
                      source: domain
                      forbidden: []
                      reason: The probe only needs a contract that forces a real analysis.
                analysis:
                  target_assemblies: [SyntheticEnsureBuiltTarget]
                  projects: [SyntheticEnsureBuiltTarget.csproj]
                  configuration: Debug
                  target_framework: net10.0
                """);
        }

        private void WriteEnsureBuiltHarness(string consumerDirectory)
        {
            File.WriteAllText(Path.Combine(consumerDirectory, "NuGet.Config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="candidate" value="{_feed}" />
                  </packageSources>
                </configuration>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "EnsureBuiltConsumer.csproj"), $"""
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
                using System.Security.Cryptography;
                using ArchLinterNet.Testing;

                string target = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "target");
                target = Path.GetFullPath(target);
                string policy = Path.Combine(target, "dependencies.arch.yml");

                // The consumer builds once, exactly as it would before running its architecture tests.
                if (Run("build", target) != 0)
                {
                    Console.WriteLine("target-build-failed");
                    return 2;
                }

                Dictionary<string, string> before = Outputs(target);
                if (before.Count == 0 || !before.Keys.Any(name => name.EndsWith(".pdb", StringComparison.Ordinal)))
                {
                    Console.WriteLine("target-outputs-missing");
                    return 3;
                }

                ArchitectureValidationResult first = new ArchitectureValidationBuilder(policy)
                    .WithEnsureBuilt()
                    .ValidateStrict();
                if (!first.Passed)
                {
                    Console.WriteLine("first-validation-failed");
                    return 4;
                }

                Console.WriteLine("first-validation-completed");

                // No rebuild in between: the second validation must find the same verified artifacts.
                ArchitectureValidationResult second = new ArchitectureValidationBuilder(policy)
                    .WithEnsureBuilt()
                    .ValidateStrict();
                if (!second.Passed)
                {
                    Console.WriteLine("second-validation-failed");
                    return 5;
                }

                Console.WriteLine("second-validation-completed");

                Dictionary<string, string> after = Outputs(target);
                if (after.Count != before.Count || after.Any(entry =>
                        !before.TryGetValue(entry.Key, out string? digest) || digest != entry.Value))
                {
                    Console.WriteLine("primary-outputs-changed");
                    return 6;
                }

                Console.WriteLine("primary-outputs-preserved");
                return 0;

                static Dictionary<string, string> Outputs(string root)
                {
                    string[] patterns = ["*.dll", "*.pdb", "*.deps.json", "*.runtimeconfig.json"];
                    string bin = Path.Combine(root, "bin");
                    if (!Directory.Exists(bin))
                    {
                        return new Dictionary<string, string>(StringComparer.Ordinal);
                    }

                    return patterns
                        .SelectMany(pattern => Directory.EnumerateFiles(bin, pattern, SearchOption.AllDirectories))
                        .ToDictionary(
                            path => Path.GetRelativePath(bin, path).Replace('\\', '/'),
                            path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                            StringComparer.Ordinal);
                }

                static int Run(string verb, string workingDirectory)
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo("dotnet")
                    {
                        WorkingDirectory = workingDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    };
                    startInfo.ArgumentList.Add(verb);
                    startInfo.ArgumentList.Add("--nologo");
                    startInfo.ArgumentList.Add("--verbosity");
                    startInfo.ArgumentList.Add("quiet");
                    using var process = System.Diagnostics.Process.Start(startInfo)!;
                    process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return process.ExitCode;
                }
                """);
        }
    }
}
