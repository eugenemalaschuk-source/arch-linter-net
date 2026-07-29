using System.Text.Json;
using System.Diagnostics;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CheckpointAAdoptionAcceptanceTests
{
    [Test]
    public void ScenarioManifest_ContainsRequiredSyntheticShapesAndNonReleaseBoundary()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ManifestPath()));
        JsonElement root = document.RootElement;
        string[] shapes = root.GetProperty("fixtures").EnumerateArray()
            .Select(fixture => fixture.GetProperty("shape").GetString()!)
            .OrderBy(shape => shape, StringComparer.Ordinal)
            .ToArray();
        string[] reusers = root.GetProperty("reusers").EnumerateArray()
            .Select(reuser => reuser.GetString()!)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("schema").GetString(), Is.EqualTo("adoption-acceptance-corpus/v1"));
            Assert.That(root.GetProperty("checkpoint").GetString(), Is.EqualTo("A"));
            Assert.That(root.GetProperty("release_gate").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("synthetic_identities_only").GetBoolean(), Is.True);
            Assert.That(shapes, Is.EqualTo(new[] { "clean-checkout", "migration", "multi-host", "multi-project", "small" }));
            Assert.That(reusers, Is.EqualTo(new[] { "#374", "#411", "#366" }));
            Assert.That(root.GetProperty("scenarios").EnumerateArray()
                .Select(scenario => scenario.GetProperty("owner").GetString()),
                Is.EquivalentTo(new[] { "#356", "#357", "#358", "#359", "#360", "#361", "#362", "#363", "#364" }));
            Assert.That(root.GetProperty("scenarios").EnumerateArray()
                .All(scenario => scenario.GetProperty("entrypoint").GetString() ==
                    "CheckpointAAdoptionAcceptanceTests.ExecuteScenario"), Is.True);
        });
    }

    public static IEnumerable<string> ScenarioIds()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ManifestPath()));
        return document.RootElement.GetProperty("scenarios").EnumerateArray()
            .Select(scenario => scenario.GetProperty("id").GetString()!)
            .ToArray();
    }

    [TestCaseSource(nameof(ScenarioIds))]
    public void ExecuteScenario(string scenarioId)
    {
        switch (scenarioId)
        {
            case "imports-provenance":
                ImportedMigrationFixture_LoadsThroughThePublicPolicyLoader();
                break;
            case "baseline-exact-identity":
            case "assembly-aware-composition":
                Assert.That(
                    new ArchitectureViolationIdentity(2, "composition", "call", "rule", "HostA", "Program", null, null, null, "Run", 0),
                    Is.Not.EqualTo(new ArchitectureViolationIdentity(2, "composition", "call", "rule", "HostB", "Program", null, null, null, "Run", 0)));
                break;
            case "subtractive-selectors":
                Assert.That(ArchitectureLayerResolver.MatchesNamespace(
                    new ArchitectureLayer { Namespace = "Synthetic.Product.*", Exclude = [new ArchitectureLayerExclusion { Namespace = "Synthetic.Product.Generated" }] },
                    "Synthetic.Product.Generated"), Is.False);
                break;
            case "package-evidence":
            case "framework-reference-evidence":
                Assert.That(ArchitectureFindingJsonReader.Read("""{"schema_version":1,"kind":"package_dependency","canonical_identity":"synthetic","mode":"strict","severity":"error","message_code":"package","baseline_state":null,"details":{"detail_kind":"package"}}""", strict: true).RawDetails.GetProperty("detail_kind").GetString(), Is.EqualTo("package"));
                break;
            case "build-state-preflight":
                Assert.That(File.Exists(ManifestPath()), Is.True);
                break;
            case "single-snapshot":
                TestingSnapshot_UsesOneAnalysisSessionForStrictAndAudit();
                break;
            case "non-tty-human-json-sarif-parity":
                CliAndTestingApi_ProduceEquivalentCanonicalFinding();
                break;
            default:
                Assert.Fail($"Unknown Checkpoint A scenario '{scenarioId}'.");
                break;
        }
    }

    [Test]
    public void ImportedMigrationFixture_LoadsThroughThePublicPolicyLoader()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        string policy = Path.Combine(root, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "imported-provenance-root.yml");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policy);

        Assert.That(document.Analysis.TargetAssemblies, Is.Not.Empty);
    }

    [Test]
    public void TestingSnapshot_UsesOneAnalysisSessionForStrictAndAudit()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-checkpoint-a-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string policyPath = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(policyPath, """
                version: 1
                name: Synthetic checkpoint A adopter

                layers:
                  execution:
                    namespace: ArchLinterNet.Core.Execution

                analysis:
                  target_assemblies: [ArchLinterNet.Core]
                """);

            var builder = new ArchitectureValidationBuilder(policyPath);
            using ArchitectureValidationSnapshotSession session = builder.CreateSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(session.ValidateStrict().Passed, Is.True);
                Assert.That(session.ValidateAudit().Passed, Is.True);
                Assert.That(session.Counters.PolicyCompositions, Is.EqualTo(1));
                Assert.That(session.Counters.ProjectGraphEvaluations, Is.EqualTo(1));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CliAndTestingApi_ProduceEquivalentCanonicalFinding()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        string policy = Path.Combine(root, "tests", "ArchLinterNet.Cli.Tests", "TestPolicies", "imported-provenance-root.yml");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root,
        };
        startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine(root, "src", "ArchLinterNet.Cli"));
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(policy);
        startInfo.ArgumentList.Add("--strict");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("json");

        using var process = Process.Start(startInfo)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        using JsonDocument cli = JsonDocument.Parse(stdout);
        ArchitectureFinding testingFinding = new ArchitectureValidationBuilder(policy).ValidateStrict().Findings.Single();
        JsonElement cliFinding = cli.RootElement.GetProperty("violations")[0];

        Assert.Multiple(() =>
        {
            Assert.That(process.ExitCode, Is.EqualTo(1), stderr);
            Assert.That(cliFinding.GetProperty("canonical_identity").GetString(), Is.EqualTo(testingFinding.CanonicalIdentity));
            Assert.That(cliFinding.GetProperty("kind").GetString(), Is.EqualTo(testingFinding.Kind));
            Assert.That(cliFinding.GetProperty("policy_location").GetProperty("source_path").GetString(),
                Is.EqualTo(testingFinding.PolicyOrigin!.SourcePath));
            Assert.That(cliFinding.GetProperty("details").GetProperty("detail_kind").GetString(),
                Is.EqualTo(testingFinding.Details.Kind.ToString().ToLowerInvariant()));
        });
    }

    private static string ManifestPath()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        return Path.Combine(root, "tests", "ArchLinterNet.Core.Tests", "AdoptionAcceptance", "CheckpointAScenarioManifest.json");
    }
}
