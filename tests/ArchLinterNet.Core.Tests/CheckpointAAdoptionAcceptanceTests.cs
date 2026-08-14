using System.Diagnostics;
using System.Text.Json;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("E2E")]
[CancelAfter(120_000)]
public sealed class CheckpointAAdoptionAcceptanceTests
{
    private static readonly string[] _value = { "#374", "#411", "#366" };
    private static readonly string[] _value1 = { "#356", "#357", "#358", "#359", "#360", "#361", "#362", "#363", "#364" };
    private static readonly string[] _value2 = { "Synthetic.Legacy" };
    private static readonly string[] _fixtureShapes =
        { "clean-checkout", "large-multi-host", "migration", "multi-host", "multi-project", "small" };

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
            Assert.That(shapes, Is.EqualTo(_fixtureShapes));
            Assert.That(reusers, Is.EqualTo(_value));
            Assert.That(root.GetProperty("scenarios").EnumerateArray()
                .Select(scenario => scenario.GetProperty("owner").GetString()),
                Is.EquivalentTo(_value1));
            Assert.That(root.GetProperty("scenarios").EnumerateArray()
                .All(scenario => !string.IsNullOrWhiteSpace(scenario.GetProperty("entrypoint").GetString())), Is.True);
            Assert.That(root.GetProperty("scenarios").EnumerateArray()
                .Single(scenario => scenario.GetProperty("owner").GetString() == "#364")
                .GetProperty("entrypoint").GetString(),
                Is.EqualTo("ValidateCommandHandlerReportModeTests.CheckpointA_HumanJsonAndSarifSinks_ExecuteOneAnalysis"));
            Assert.That(DeclaredFixtureRoots(root), Is.EqualTo(_fixtureShapes));
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
                new ArchitectureBaselineComparerTests()
                    .Compare_Version1Baseline_UnqualifiedIdentityStillMatchesByLegacyPair();
                break;
            case "subtractive-selectors":
                using (AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("small"))
                {
                    fixture.Build();
                }
                Assert.That(ArchitectureLayerResolver.MatchesNamespace(
                    new ArchitectureLayer { Namespace = "Synthetic.Product.*", Exclude = [new ArchitectureLayerExclusion { Namespace = "Synthetic.Product.Generated" }] },
                    "Synthetic.Product.Generated"), Is.False);
                break;
            case "package-evidence":
                new ArchitectureDiagnosticFormatterTests()
                    .GroupedPackageViolation_EachAdapterAlignsEvidenceWithCanonicalIdentity();
                break;
            case "framework-reference-evidence":
                using (AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("multi-project"))
                {
                    fixture.Build();
                }
                new ArchitectureDiagnosticFormatterTests()
                    .FrameworkReferenceViolation_WithEvidence_HumanJsonAndSarifRenderStructuredFields();
                break;
            case "assembly-aware-composition":
                using (AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("multi-host"))
                {
                    fixture.Build();
                }
                var composition = new CompositionContractTests();
                composition.SetUp();
                try
                {
                    composition.CheckCompositionContract_Violation_BaselineCandidateIsAssemblyAndMemberQualified();
                    composition.CheckCompositionContract_TwoCallsToSameApiInSameMember_BaseliningFirstDoesNotSuppressSecond();
                }
                finally
                {
                    composition.TearDown();
                }
                break;
            case "build-state-preflight":
                CleanCheckoutFixture_ReportsMissingArtifact();
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
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("migration");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(fixture.PolicyPath);

        Assert.Multiple(() =>
        {
            Assert.That(document.Analysis.TargetAssemblies, Is.EqualTo(_value2));
            Assert.That(document.Layers, Contains.Key("legacy"));
            Assert.That(File.ReadAllText(Path.Combine(fixture.Root, "baseline.yml")), Does.StartWith("version: 1"));
        });
    }

    [TestCase("small")]
    [TestCase("multi-project")]
    [TestCase("multi-host")]
    [TestCase("migration")]
    public void FixtureRoot_IsCompilableAndContainsSyntheticSources(string fixtureId)
    {
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create(fixtureId);

        fixture.Build();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ProjectPaths, Is.Not.Empty);
            Assert.That(fixture.SourcePaths, Is.Not.Empty);
        });
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
        startInfo.ArgumentList.Add(Path.Combine(
            root, "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll"));
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

    internal static string ManifestPath()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        return Path.Combine(root, "tests", "ArchLinterNet.Core.Tests", "AdoptionAcceptance", "CheckpointAScenarioManifest.json");
    }

    private static string[] DeclaredFixtureRoots(JsonElement manifest)
    {
        string manifestDirectory = Path.GetDirectoryName(ManifestPath())!;
        string[] roots = manifest.GetProperty("fixtures").EnumerateArray()
            .Select(fixture => fixture.GetProperty("root").GetString()!)
            .ToArray();
        foreach (string relativeRoot in roots)
        {
            string root = Path.Combine(manifestDirectory, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(Path.Combine(root, "dependencies.arch.yml")), Is.True, relativeRoot);
                Assert.That(Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories), Is.Not.Empty, relativeRoot);
                Assert.That(Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories), Is.Not.Empty, relativeRoot);
                Assert.That(Directory.GetDirectories(root, "bin", SearchOption.AllDirectories), Is.Empty, relativeRoot);
                Assert.That(Directory.GetDirectories(root, "obj", SearchOption.AllDirectories), Is.Empty, relativeRoot);
            });
        }

        return roots.Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray()!;
    }

    private static void CleanCheckoutFixture_ReportsMissingArtifact()
    {
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("clean-checkout");

        ArchitectureValidationResult result = new ArchitectureValidationBuilder(fixture.PolicyPath).ValidateStrict();

        Assert.Multiple(() =>
        {
            Assert.That(result.PreflightBlocked, Is.True);
            Assert.That(result.PreflightDiagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.MissingArtifact));
            Assert.That(Directory.GetDirectories(fixture.Root, "bin", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.GetDirectories(fixture.Root, "obj", SearchOption.AllDirectories), Is.Empty);
        });
    }
}
