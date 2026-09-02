using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Topology;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class TopologyCommandHandlerTests
{
    private static readonly string[] _topologySubcommandNames = ["capture", "diff", "verify"];
    private static readonly string[] _sharedValidationEvidenceArguments =
    [
        "--waiver-evaluation-date", "2026-09-02",
        "--external-evidence", "id=external.unknown,path=evidence/scan.sarif",
        "--evidence-repository", "repo",
        "--evidence-revision", "revision",
        "--evidence-scope", "ci",
    ];

    [Test]
    public void TopologyModule_ComposesCaptureDiffAndVerifySubcommands()
    {
        Command command = new TopologyCommandModule().CreateCommand(new FakeRuntime(), new FakeConsole(), new FakeFileSystem());

        Assert.That(command.Subcommands.Select(subcommand => subcommand.Name),
            Is.EqualTo(_topologySubcommandNames));
    }

    [Test]
    public void CaptureJson_IsByteStableAndOrdersFacts()
    {
        FakeConsole console = new();
        FakeFileSystem files = new();
        FakeRuntime runtime = new()
        {
            CaptureResult = new ArchitectureTopologyCaptureOutcome(
                "assembly",
                [
                    new("b", "assembly", "B", "PB", "AB"),
                    new("a", "assembly", "A", "PA", "AA"),
                ],
                [new("b", "a", "B -> A"), new("a", "b", "A -> B")],
                "repo",
                ["z.yml", "a.yml"], ["z.dll", "a.dll"], ["z.csproj", "a.csproj"],
                [],
                false),
        };
        TopologyCommandHandler handler = new(runtime, console, files);
        TopologyCaptureCommandOptions options = new("policy.yml", "assembly", "json", null, null, false);

        Assert.That(handler.Capture(options), Is.EqualTo(CliExitCodes.Success));
        string first = console.Output;
        console.Clear();
        Assert.That(handler.Capture(options), Is.EqualTo(CliExitCodes.Success));

        Assert.That(console.Output, Is.EqualTo(first));
        using JsonDocument document = JsonDocument.Parse(first);
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("topology-capture"));
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("subjects")[0].GetProperty("identity").GetString(), Is.EqualTo("a"));
            Assert.That(document.RootElement.GetProperty("policy_import_paths")[0].GetString(), Is.EqualTo("a.yml"));
        });
    }

    [Test]
    public void CaptureRenderers_IncludeSortedReviewFactsAndEveryPreflightState()
    {
        BuildStatePreflightDiagnostic[] diagnostics = Enum.GetValues<BuildStatePreflightState>()
            .Select((state, index) => new BuildStatePreflightDiagnostic(
                $"contract-{index:D2}", $"id-{index:D2}", state,
                new BuildStatePreflightEvidence(
                    "project.csproj", "Assembly", "Release", "Debug", "net10.0", "net9.0",
                    "output.dll", ["z/path", "a/path"], "dotnet build", "detail", "cache-eligible",
                    ["z-reason", "a-reason"])))
            .ToArray();
        FakeRuntime runtime = new()
        {
            CaptureResult = new ArchitectureTopologyCaptureOutcome(
                "assembly", [new("b", "assembly", "B", "Project", "Assembly")],
                [new("b", "a", "B -> A")], "repo", ["z.yml", "a.yml"], ["z.dll", "a.dll"],
                ["z.csproj", "a.csproj"], diagnostics, true),
        };
        FakeConsole console = new();
        TopologyCommandHandler handler = new(runtime, console, new FakeFileSystem());

        Assert.That(handler.Capture(new("policy.yml", "assembly", "human", null, null, false)),
            Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.Multiple(() =>
        {
            Assert.That(console.Output, Does.Contain("subject: B [b]").And.Contain("relationship: b -> a"));
            Assert.That(console.Output, Does.Contain("Preflight blocked: True"));
            Assert.That(console.Output, Does.Contain("preflight: Current contract-09"));
        });

        console.Clear();
        Assert.That(handler.Capture(new("policy.yml", "assembly", "json", null, null, false)),
            Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        using JsonDocument document = JsonDocument.Parse(console.Output);
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("preflight_diagnostics").GetArrayLength(),
                Is.EqualTo(diagnostics.Length));
            Assert.That(document.RootElement.GetProperty("preflight_diagnostics")[0]
                .GetProperty("searched_paths")[0].GetString(), Is.EqualTo("a/path"));
            Assert.That(document.RootElement.GetProperty("preflight_diagnostics")[0]
                .GetProperty("cache_ineligibility_reasons")[0].GetString(), Is.EqualTo("a-reason"));
        });
    }

    [Test]
    public void CaptureOutput_RejectsConsumedInputAfterCapture()
    {
        string policy = Path.GetFullPath("policy.yml");
        FakeConsole console = new();
        FakeFileSystem files = new();
        files.ExistingPaths.Add(policy);
        FakeRuntime runtime = new()
        {
            CaptureResult = new ArchitectureTopologyCaptureOutcome(
                "assembly", [], [], "repo", [policy], ["a.dll"], ["a.csproj"], []),
        };

        int exitCode = new TopologyCommandHandler(runtime, console, files)
            .Capture(new(policy, "assembly", "json", policy, null, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.Contain("output-collision"));
            Assert.That(runtime.CaptureCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void CaptureOutput_RejectsHardLinkToConsumedInput()
    {
        string root = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-hardlink-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string policy = Path.Combine(root, "policy.yml");
            string alias = Path.Combine(root, "capture.json");
            File.WriteAllText(policy, "trusted policy");
            CreateHardLink(alias, policy);
            FakeConsole console = new();
            FakeRuntime runtime = new()
            {
                CaptureResult = new ArchitectureTopologyCaptureOutcome(
                    "assembly", [], [], root, [policy], [], [], []),
            };

            int exitCode = new TopologyCommandHandler(runtime, console, new FileSystem())
                .Capture(new(policy, "assembly", "json", alias, null, false));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
                Assert.That(console.Output, Does.Contain("output-collision"));
                Assert.That(File.ReadAllText(policy), Is.EqualTo("trusted policy"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void CaptureOutput_RejectsConsumedCSharpSourceInput()
    {
        string root = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        try
        {
            string source = Path.Combine(root, "src", "Consumer.cs");
            File.WriteAllText(source, "namespace Consumer;");
            FakeConsole console = new();
            FakeRuntime runtime = new()
            {
                CaptureResult = new ArchitectureTopologyCaptureOutcome("assembly", [], [], root, [], [], [], [])
                {
                    ConsumedInputPaths = [source],
                },
            };

            int exitCode = new TopologyCommandHandler(runtime, console, new FileSystem())
                .Capture(new("policy.yml", "assembly", "json", source, null, false));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
                Assert.That(console.Output, Does.Contain("output-collision"));
                Assert.That(File.ReadAllText(source), Is.EqualTo("namespace Consumer;"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void CaptureOutput_PublicationFailurePreservesTargetAndCleansTemporaryFile()
    {
        FakeConsole console = new();
        FakeFileSystem files = new() { ThrowOnRename = true };
        FakeRuntime runtime = new()
        {
            CaptureResult = new ArchitectureTopologyCaptureOutcome("assembly", [], [], "repo", [], [], [], []),
        };

        int exitCode = new TopologyCommandHandler(runtime, console, files)
            .Capture(new("policy.yml", "assembly", "json", "capture.json", null, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.Contain("output-write-failed"));
            Assert.That(files.RenameCalls, Is.EqualTo(1));
            Assert.That(files.DeletedPaths, Is.EqualTo(new[] { "capture.json.tmp" }));
            Assert.That(files.DirectWrites, Is.EqualTo(0));
        });
    }

    [Test]
    public void Diff_ProjectsDistinctCategoriesAndCallsValidationOnce()
    {
        FakeConsole console = new();
        ArchitectureTopologyMappingEvidence evidence = new(
            "exhaustive", "assembly", 3,
            [
                new("amb", "P", "A", "Ambiguous", "ambiguous", ["one", "two"]),
                new("unmapped", "P", "A", "Unmapped", "unmapped"),
                new("out", "P", "A", "Out", "reviewed_out_of_scope", [], "review"),
            ],
            [new("one", "two", "A -> B", false), new("one", "three", "A -> C", true)],
            ["retired"], [new("retired", "old")]);
        FakeRuntime runtime = new() { ValidationResult = Outcome(evidence, passed: false) };
        int exitCode = new TopologyCommandHandler(runtime, console, new FakeFileSystem())
            .Diff(new("policy.yml", "strict", "json", null, null, null, [], false));

        using JsonDocument document = JsonDocument.Parse(console.Output);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.ValidateCalls, Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("structural").GetArrayLength(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("relational")[0].GetProperty("witness").GetString(), Is.EqualTo("A -> B"));
            Assert.That(document.RootElement.GetProperty("unmapped").GetArrayLength(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("stale").GetProperty("nodes")[0].GetString(), Is.EqualTo("retired"));
            Assert.That(document.RootElement.GetProperty("reviewed_out_of_scope").GetArrayLength(), Is.EqualTo(1));
        });
    }

    [Test]
    public void DiffHuman_ListsEveryReviewCategory()
    {
        ArchitectureTopologyMappingEvidence evidence = new(
            "exhaustive", "assembly", 3,
            [
                new("amb", "P", "A", "Ambiguous", "ambiguous", ["one", "two"]),
                new("unmapped", "P", "A", "Unmapped", "unmapped"),
                new("out", "P", "A", "Out", "reviewed_out_of_scope", [], "review"),
            ],
            [new("one", "two", "A -> B", false)], ["retired"], [new("retired", "old")]);
        FakeConsole console = new();

        int exitCode = new TopologyCommandHandler(
            new FakeRuntime { ValidationResult = Outcome(evidence, passed: false) }, console, new FakeFileSystem())
            .Diff(new("policy.yml", "strict", "human", null, null, null, [], false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("structural: Ambiguous")
                .And.Contain("relational: one -> two")
                .And.Contain("unmapped: Unmapped")
                .And.Contain("stale node: retired")
                .And.Contain("stale edge: retired -> old")
                .And.Contain("reviewed out of scope: Out"));
        });
    }

    [Test]
    public void Diff_UnassessableEmptyInputPreservesApplicabilityAndReturnsRuntimeError()
    {
        ArchitectureTopologyMappingEvidence evidence = new("exhaustive", "assembly", 0, [], [], [], []);
        ArchitectureApplicabilityProvenance provenance = new(
            "declared_topology", "declared-topology", "policy-v08");
        ArchitectureApplicabilityRecord record = new(
            "declared-topology",
            "declared_topology",
            ArchitectureApplicabilityRecordState.Unassessable,
            [new ArchitectureApplicabilityReason(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput, provenance)],
            provenance)
        {
            TopologyEvidence = evidence,
        };
        ValidationOutcome outcome = OutcomeForRecord(record, ArchitectureApplicabilityMembership.Required, passed: false);
        FakeConsole console = new();

        int exitCode = new TopologyCommandHandler(new FakeRuntime { ValidationResult = outcome }, console, new FakeFileSystem())
            .Diff(new("policy.yml", "strict", "json", null, null, null, [], false));

        using JsonDocument document = JsonDocument.Parse(console.Output);
        JsonElement applicability = document.RootElement.GetProperty("applicability");
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(document.RootElement.GetProperty("outcome").GetString(), Is.EqualTo("unassessable"));
            Assert.That(applicability.GetProperty("state").GetString(), Is.EqualTo("unassessable"));
            Assert.That(applicability.GetProperty("membership").GetString(), Is.EqualTo("required"));
            Assert.That(applicability.GetProperty("provenance").GetProperty("policy_identity").GetString(),
                Is.EqualTo("policy-v08"));
            Assert.That(applicability.GetProperty("reasons")[0].GetProperty("code").GetString(),
                Is.EqualTo(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput));
        });
    }

    [Test]
    public void Diff_ProjectableUnmappedEvidenceRemainsReviewable()
    {
        ArchitectureTopologyMappingEvidence evidence = new(
            "exhaustive", "assembly", 1,
            [new("unmapped", "Project", "Assembly", "Unmapped", "unmapped")], [], [], []);
        ArchitectureApplicabilityProvenance provenance = new("declared_topology", "declared-topology");
        ArchitectureApplicabilityRecord record = new(
            "declared-topology",
            "declared_topology",
            ArchitectureApplicabilityRecordState.Unassessable,
            [new ArchitectureApplicabilityReason(ArchitectureApplicabilityReasonCodes.UnmappedSubject, provenance)],
            provenance)
        {
            TopologyEvidence = evidence,
        };
        FakeConsole console = new();

        int exitCode = new TopologyCommandHandler(
                new FakeRuntime { ValidationResult = OutcomeForRecord(record, ArchitectureApplicabilityMembership.Required, false) },
                console,
                new FakeFileSystem())
            .Diff(new("policy.yml", "strict", "json", null, null, null, [], false));

        using JsonDocument document = JsonDocument.Parse(console.Output);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(document.RootElement.GetProperty("outcome").GetString(), Is.EqualTo("reviewable"));
            Assert.That(document.RootElement.GetProperty("unmapped").GetArrayLength(), Is.EqualTo(1));
        });
    }

    [Test]
    public void CaptureOutput_NewFileSkipsUnrelatedUnityLikeDirectories()
    {
        string root = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Library", "PackageCache"));
        Directory.CreateDirectory(Path.Combine(root, "Temp", "generated"));
        try
        {
            for (int index = 0; index < 32; index++)
            {
                File.WriteAllText(Path.Combine(root, "Library", "PackageCache", $"generated-{index}.cs"), "ignored");
            }

            string output = Path.Combine(root, "review", "capture.json");
            int exitCode = new TopologyCommandHandler(
                    new FakeRuntime { CaptureResult = new ArchitectureTopologyCaptureOutcome("assembly", [], [], root, [], [], [], []) },
                    new FakeConsole(),
                    new FileSystem())
                .Capture(new("policy.yml", "assembly", "json", output, null, false));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
                Assert.That(File.Exists(output), Is.True);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void CaptureOutput_CancellationDuringPublicationIsTypedAndCleansTemporaryFile()
    {
        using CancellationTokenSource cancellation = new();
        FakeFileSystem files = new() { OnWriteTemporaryFile = cancellation.Cancel };
        FakeConsole console = new();

        int exitCode = new TopologyCommandHandler(
                new FakeRuntime { CaptureResult = new ArchitectureTopologyCaptureOutcome("assembly", [], [], "repo", [], [], [], []) },
                console,
                files,
                cancellation.Token)
            .Capture(new("policy.yml", "assembly", "json", "capture.json", null, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.Contain("cancelled").And.Not.Contain("output-write-failed"));
            Assert.That(files.RenameCalls, Is.Zero);
            Assert.That(files.DeletedPaths, Is.EqualTo(new[] { "capture.json.tmp" }));
        });
    }

    [Test]
    public void Verify_UsesNormalJsonEnvelopeAndPreservesAuditMode()
    {
        ArchitectureTopologyMappingEvidence evidence = new(
            "partial", "assembly", 1, [], [], [], []);
        FakeRuntime runtime = new()
        {
            ValidationResult = Outcome(evidence, passed: true),
            ValidationJson = "{\"passed\":true,\"mode\":\"audit\",\"violations\":[]}",
        };
        FakeConsole console = new();
        int exitCode = new TopologyCommandHandler(runtime, console, new FakeFileSystem())
            .Verify(new("policy.yml", "audit", "json", null, null, null, [], false));

        using JsonDocument document = JsonDocument.Parse(console.Output);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.ValidateCalls, Is.EqualTo(1));
            Assert.That(runtime.LastValidationRequest!.Mode, Is.EqualTo("audit"));
            Assert.That(document.RootElement.GetProperty("mode").GetString(), Is.EqualTo("audit"));
            Assert.That(document.RootElement.TryGetProperty("kind", out _), Is.False);
        });
    }

    [Test]
    public void Verify_AppliesOrdinaryExternalEvidenceAndWaiverSemantics()
    {
        string root = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "evidence"));
        try
        {
            File.WriteAllText(Path.Combine(root, "evidence", "scan.sarif"),
                """
                {"version":"2.1.0","runs":[{"tool":{"driver":{"name":"Synthetic.Scanner","version":"1.0","rules":[{"id":"SEC100"}]}},"automationDetails":{"id":"assessment-42"},"invocations":[{"executionSuccessful":true}],"results":[{"ruleId":"SEC100","level":"error","message":{"text":"finding"}}]}]}
                """);
            ArchitectureTopologyMappingEvidence topology = new("partial", "assembly", 1, [], [], [], []);
            ValidationOutcome outcome = Outcome(topology, passed: true) with
            {
                RepositoryRoot = root,
                ExternalEvidenceRequirements =
                [
                    new ArchitectureExternalEvidenceRequirement
                    {
                        Id = "external.scan",
                        Format = "sarif",
                        Required = true,
                        Tool = "Synthetic.Scanner",
                        ToolVersion = "1.0",
                        Run = "assessment-42",
                        DiagnosticFilter = new ArchitectureExternalEvidenceDiagnosticFilter
                        {
                            Severity = new Dictionary<string, string> { ["error"] = "strict" },
                        },
                    },
                ],
                ApplicabilityExpectedEntries =
                [
                    new ArchitectureApplicabilityExpectedEntry(
                        "declared-topology",
                        "declared_topology",
                        ArchitectureApplicabilityMembership.Required,
                        new ArchitectureApplicabilityProvenance(
                            "declared_topology", "declared-topology", "declared-topology")),
                ],
            };
            FakeRuntime runtime = new() { ValidationResult = outcome };
            FakeConsole console = new();
            TopologyValidationCommandOptions options = new("policy.yml", "strict", "json", null, null, null, [], false)
            {
                WaiverEvaluationDate = "2026-09-02",
                ExternalEvidenceArtifacts = [new SarifEvidenceArtifactReference("evidence/scan.sarif", "external.scan")],
            };

            int exitCode = new TopologyCommandHandler(runtime, console, new FakeFileSystem()).Verify(options);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.ValidationFailure), console.Output + console.ErrorText);
                Assert.That(runtime.LastValidationRequest!.WaiverEvaluationDate, Is.EqualTo(new DateOnly(2026, 9, 2)));
                Assert.That(runtime.ValidateCalls, Is.EqualTo(1));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestCase("diff")]
    [TestCase("verify")]
    public void TopologySubcommands_ParseSharedValidationEvidenceInputs(string subcommand)
    {
        ArchitectureTopologyMappingEvidence topology = new("partial", "assembly", 1, [], [], [], []);
        FakeRuntime runtime = new() { ValidationResult = Outcome(topology, passed: true) };
        FakeConsole console = new();
        Command command = new TopologyCommandModule().CreateCommand(runtime, console, new FakeFileSystem());

        int exitCode = command.Parse([subcommand, .. _sharedValidationEvidenceArguments]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(runtime.ValidateCalls, Is.EqualTo(1));
            Assert.That(runtime.LastValidationRequest!.WaiverEvaluationDate, Is.EqualTo(new DateOnly(2026, 9, 2)));
            Assert.That(console.Output + console.ErrorText, Does.Contain("does not match a declared"));
        });
    }

    [Test]
    public void TopologyHelp_AdvertisesOnlyRegisteredOptions()
    {
        FakeConsole console = new();
        TopologyCommandHandler handler = new(new FakeRuntime(), console, new FakeFileSystem());

        Assert.That(handler.Capture(new("policy.yml", "assembly", "human", null, null, true)),
            Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.Output, Does.Not.Contain("--waiver-evaluation-date").And.Not.Contain("--external-evidence"));

        console.Clear();
        Assert.That(handler.Diff(new("policy.yml", "strict", "human", null, null, null, [], true)),
            Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.Output, Does.Contain("--waiver-evaluation-date").And.Contain("--external-evidence")
            .And.Contain("--evidence-repository").And.Contain("--evidence-revision").And.Contain("--evidence-scope"));

        Command command = new TopologyCommandModule().CreateCommand(new FakeRuntime(), new FakeConsole(), new FakeFileSystem());
        Assert.Multiple(() =>
        {
            Assert.That(command.Parse(["capture", "--waiver-evaluation-date", "2026-09-02"]).Errors, Is.Not.Empty);
            Assert.That(command.Parse(["diff", .. _sharedValidationEvidenceArguments]).Errors, Is.Empty);
            Assert.That(command.Parse(["verify", .. _sharedValidationEvidenceArguments]).Errors, Is.Empty);
        });
    }

    [Test]
    public void DiffWithoutTopology_IsTypedInputErrorAfterOneValidation()
    {
        FakeRuntime runtime = new() { ValidationResult = Outcome(null, passed: true) };
        FakeConsole console = new();

        int exitCode = new TopologyCommandHandler(runtime, console, new FakeFileSystem())
            .Diff(new("policy.yml", "strict", "json", null, null, null, [], false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(runtime.ValidateCalls, Is.EqualTo(1));
            Assert.That(console.Output, Does.Contain("no-declared-topology"));
        });
    }

    private static ValidationOutcome Outcome(ArchitectureTopologyMappingEvidence? evidence, bool passed)
    {
        ArchitectureApplicabilityRecord[] records = evidence is null
            ? []
            : [new ArchitectureApplicabilityRecord("declared-topology", "declared_topology",
                ArchitectureApplicabilityRecordState.Evaluable,
                new ArchitectureApplicabilityProvenance(
                    "declared_topology", "declared-topology", "declared-topology"))
            {
                TopologyEvidence = evidence,
            }];
        return new ValidationOutcome(passed, [], [], [], "off", [], "off", [], "off", [], [], [])
        {
            ApplicabilityRecords = records,
            ApplicabilityExpectedEntries = evidence is null
                ? []
                : [new ArchitectureApplicabilityExpectedEntry(
                    "declared-topology",
                    "declared_topology",
                    ArchitectureApplicabilityMembership.Required,
                    new ArchitectureApplicabilityProvenance(
                        "declared_topology", "declared-topology", "declared-topology"))],
            RepositoryRoot = Path.GetFullPath("."),
        };
    }

    private static ValidationOutcome OutcomeForRecord(
        ArchitectureApplicabilityRecord record,
        ArchitectureApplicabilityMembership membership,
        bool passed) => new ValidationOutcome(passed, [], [], [], "off", [], "off", [], "off", [], [], [])
        {
            ApplicabilityRecords = [record],
            ApplicabilityExpectedEntries = [new ArchitectureApplicabilityExpectedEntry(
                record.ControlIdentity,
                record.Family,
                membership,
                record.Provenance)],
            RepositoryRoot = Path.GetFullPath("."),
        };

    private static void CreateHardLink(string linkPath, string existingPath)
    {
        bool created = OperatingSystem.IsWindows()
            ? CreateHardLinkWindows(linkPath, existingPath, IntPtr.Zero)
            : CreateHardLinkUnix(existingPath, linkPath) == 0;
        if (!created)
        {
            throw new IOException("The test host could not create a hard-link alias.");
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkWindows(string linkPath, string existingPath, IntPtr securityAttributes);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int CreateHardLinkUnix(string existingPath, string linkPath);

    private sealed class FakeRuntime : ICliRuntime
    {
        public string Version => "test";
        public ArchitectureTopologyCaptureOutcome CaptureResult { get; init; } = new("assembly", [], [], "repo", [], [], [], []);
        public ValidationOutcome ValidationResult { get; init; } = Outcome(null, true);
        public string ValidationJson { get; init; } = "{\"passed\":true,\"mode\":\"strict\",\"violations\":[]}";
        public int CaptureCalls { get; private set; }
        public int ValidateCalls { get; private set; }
        public ValidationRequest? LastValidationRequest { get; private set; }
        public ArchitectureTopologyCaptureOutcome CaptureTopology(ArchitectureTopologyCaptureRequest request)
        {
            CaptureCalls++;
            return CaptureResult;
        }
        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
        {
            ValidateCalls++;
            LastValidationRequest = request;
            return ValidationResult;
        }
        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => Enum.TryParse(value, true, out level);
        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) => throw new NotSupportedException();
        public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureViolation> coverageFindings, IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations, IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings, IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries, IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => ValidationJson;
        public string FormatResultAsSarif(string mode, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => "{}";
        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) => string.Empty;
        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => string.Empty;
        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => string.Empty;
        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => string.Empty;
        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => string.Empty;
        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => string.Empty;
        public string FormatClassificationFactsForHumans(IReadOnlyCollection<ArchitectureClassificationConflict> conflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => string.Empty;
        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => string.Empty;
        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request) => throw new NotSupportedException();
        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request) => throw new NotSupportedException();
        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request) => throw new NotSupportedException();
        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request) => throw new NotSupportedException();
        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request) => throw new NotSupportedException();
        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request) => throw new NotSupportedException();
        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => "{}";
        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => string.Empty;
        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => string.Empty;
        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => throw new NotSupportedException();
        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => throw new NotSupportedException();
        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request) => throw new NotSupportedException();
        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) => throw new NotSupportedException();
        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request) => throw new NotSupportedException();
        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request) => throw new NotSupportedException();
    }

    private sealed class FakeConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
        public string Output => _output.ToString();
        public string ErrorText => _error.ToString();
        public void Clear() { _output.Clear(); _error.Clear(); }
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        public bool ThrowOnRename { get; init; }
        public Action? OnWriteTemporaryFile { get; init; }
        public int RenameCalls { get; private set; }
        public int DirectWrites { get; private set; }
        public List<string> DeletedPaths { get; } = [];
        public HashSet<string> ExistingPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool FileExists(string path) => ExistingPaths.Contains(Path.GetFullPath(path));
        public string ReadAllText(string path) => string.Empty;
        public void WriteAllText(string path, string contents) => DirectWrites++;
        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            OnWriteTemporaryFile?.Invoke();
            return targetPath + ".tmp";
        }
        public void RenameTempToTarget(string tempPath, string targetPath)
        {
            RenameCalls++;
            if (ThrowOnRename)
            {
                throw new IOException("simulated atomic rename failure");
            }
        }
        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => true;
        public void DeleteFile(string path) => DeletedPaths.Add(path);
        public bool TryCreateNewFile(string path) => true;
        public bool DirectoryExists(string path) => true;
        public void DeleteDirectoryIfEmpty(string path) { }
        public bool CanWriteToDirectory(string path) => true;
    }
}
