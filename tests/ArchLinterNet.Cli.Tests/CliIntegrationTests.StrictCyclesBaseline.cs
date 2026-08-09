using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    [Test]
    public void BaselineUpdateAndVerify_AcyclicStrictCycles_PersistsNoCycleEntries()
    {
        string directory = CreateCycleBaselineDirectory();
        try
        {
            string policyPath = WriteCyclePolicy(directory, "CliStrictCyclesBaselineFixtures.Acyclic");
            string baselinePath = Path.Combine(directory, "baseline.yml");
            string updatedPath = Path.Combine(directory, "updated.yml");
            File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");

            var (updateExit, _, updateStderr) = RunCli(
                "baseline", "update", "--config", policyPath, "--baseline", baselinePath, "--output", updatedPath);
            var (verifyExit, verifyJson, verifyStderr) = RunCli(
                "baseline", "verify", "--config", policyPath, "--baseline", updatedPath, "--json");

            using var verify = JsonDocument.Parse(verifyJson);
            string updated = File.ReadAllText(updatedPath);

            Assert.Multiple(() =>
            {
                Assert.That(updateExit, Is.EqualTo(0), $"Update should succeed, stderr: {updateStderr}");
                Assert.That(updated, Does.Not.Contain("strict_cycles:"));
                Assert.That(verifyExit, Is.EqualTo(0), $"Verify should succeed, stderr: {verifyStderr}");
                Assert.That(verify.RootElement.GetProperty("inSync").GetBoolean(), Is.True);
                Assert.That(verify.RootElement.GetProperty("counts").GetProperty("new").GetInt32(), Is.Zero);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BaselineUpdateAndVerify_CyclicStrictCycles_PersistsOnlyCycleEdgesAndReportsNewDebt()
    {
        string directory = CreateCycleBaselineDirectory();
        try
        {
            string policyPath = WriteCyclePolicy(directory, "CliStrictCyclesBaselineFixtures.Cyclic");
            string baselinePath = Path.Combine(directory, "baseline.yml");
            string updatedPath = Path.Combine(directory, "updated.yml");
            File.WriteAllText(baselinePath, "version: 2\nbaseline: {}\n");

            var (updateExit, _, updateStderr) = RunCli(
                "baseline", "update", "--config", policyPath, "--baseline", baselinePath, "--output", updatedPath);
            var (verifyExit, verifyJson, verifyStderr) = RunCli(
                "baseline", "verify", "--config", policyPath, "--baseline", baselinePath, "--json");

            ArchitectureBaselineDocument updated = new ArchitectureBaselineLoadingService().Load(updatedPath);
            string[] identities = updated.Baseline.StrictCycles.Single().IgnoredViolations
                .Select(ignored => $"{ignored.SourceType}->{ignored.ForbiddenReference}")
                .ToArray();
            using var verify = JsonDocument.Parse(verifyJson);

            Assert.Multiple(() =>
            {
                Assert.That(updateExit, Is.EqualTo(0), $"Update should succeed, stderr: {updateStderr}");
                Assert.That(updated.Baseline.StrictCycles, Has.Count.EqualTo(1));
                Assert.That(identities, Is.EquivalentTo(
                [
                    $"{typeof(CliStrictCyclesBaselineFixtures.Cyclic.LayerA.ServiceA).FullName}->{typeof(CliStrictCyclesBaselineFixtures.Cyclic.LayerB.ServiceB).FullName}",
                    $"{typeof(CliStrictCyclesBaselineFixtures.Cyclic.LayerB.ServiceB).FullName}->{typeof(CliStrictCyclesBaselineFixtures.Cyclic.LayerC.ServiceC).FullName}",
                    $"{typeof(CliStrictCyclesBaselineFixtures.Cyclic.LayerC.ServiceC).FullName}->{typeof(CliStrictCyclesBaselineFixtures.Cyclic.LayerA.ServiceA).FullName}",
                ]));
                Assert.That(verifyExit, Is.EqualTo(1), $"Verify should find new debt, stderr: {verifyStderr}");
                Assert.That(verify.RootElement.GetProperty("inSync").GetBoolean(), Is.False);
                Assert.That(verify.RootElement.GetProperty("counts").GetProperty("new").GetInt32(), Is.EqualTo(3));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateCycleBaselineDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-strict-cycles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteCyclePolicy(string directory, string fixtureNamespace)
    {
        string policyPath = Path.Combine(directory, "dependencies.arch.yml");
        string assemblyName = typeof(CliStrictCyclesBaselineFixtures.Cyclic.LayerA.ServiceA).Assembly.GetName().Name!;
        File.WriteAllText(policyPath, $$"""
            version: 1
            name: Strict cycles baseline CLI fixture
            layers:
              layerA:
                namespace: {{fixtureNamespace}}.LayerA
              layerB:
                namespace: {{fixtureNamespace}}.LayerB
              layerC:
                namespace: {{fixtureNamespace}}.LayerC
            analysis:
              target_assemblies: [{{assemblyName}}]
            contracts:
              strict_cycles:
                - id: cycle-fixture
                  name: cycle-fixture
                  layers: [layerA, layerB, layerC]
            """);
        return policyPath;
    }
}
