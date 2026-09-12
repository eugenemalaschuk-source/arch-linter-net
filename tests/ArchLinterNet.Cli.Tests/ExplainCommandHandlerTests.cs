using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Explain;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
internal sealed class ExplainCommandHandlerTests : ExplainCommandHandlerTestBase
{
    [Test]
    public void ShowHelp_PrintsHelpText()
    {
        var console = new RecordingCliConsole();
        int result = Handler(new ExplainStubRuntime(), console).Execute(Options(showHelp: true));

        Assert.That(result, Is.EqualTo(CliExitCodes.Success));
        Assert.That(console.OutputText, Does.Contain("arch-linter-net explain"));
    }

    [TestCase("invalid", "namespace", "human", "Invalid mode")]
    [TestCase("strict", "invalid", "human", "Invalid level")]
    [TestCase("strict", "namespace", "invalid", "Invalid format")]
    public void InvalidOption_ReturnsErrorWithMessage(string mode, string level, string format, string expectedError)
    {
        var console = new RecordingCliConsole();
        int result = Handler(new ExplainStubRuntime(), console).Execute(Options(mode: mode, level: level, format: format));

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Does.Contain(expectedError));
    }

    [TestCase(null, "B")]
    [TestCase("A", null)]
    [TestCase("", "B")]
    public void MissingSourceOrTarget_ReturnsError(string? source, string? target)
    {
        var console = new RecordingCliConsole();
        int result = Handler(new ExplainStubRuntime(), console).Execute(Options(source: source, target: target));

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Does.Contain("--source and --target are required"));
    }

    [TestCase("invalid", "namespace", "Invalid mode")]
    [TestCase("strict", "invalid", "Invalid level")]
    public void InvalidOptionWithJson_WritesStructuredError(string mode, string level, string expectedError)
    {
        var console = new RecordingCliConsole();
        int result = Handler(new ExplainStubRuntime(), console).Execute(Options(mode: mode, level: level, format: "json"));

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Is.Empty);
        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        Assert.That(document.RootElement.GetProperty("error").GetProperty("message").GetString(), Does.Contain(expectedError));
    }

    [Test]
    public void RuntimeExceptionWithJson_WritesStructuredError()
    {
        var console = new RecordingCliConsole();
        int result = Handler(new ExplainStubRuntime { ThrowException = new InvalidOperationException("engine error") }, console)
            .Execute(Options(format: "json"));

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Is.Empty);
        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        Assert.That(document.RootElement.GetProperty("error").GetProperty("message").GetString(), Does.Contain("engine error"));
    }

    [Test]
    public void TypedPolicyFailure_BypassesFileExistsAndWritesJson()
    {
        ArchitecturePolicyLoadException exception = PolicyException();
        var runtime = new ExplainStubRuntime { ThrowException = exception };
        var console = new RecordingCliConsole();
        int result = Handler(runtime, console).Execute(Options(format: "json"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.OutputText, Does.Contain("architecture_policy_error").And.Contain("architecture/root.yml"));
            Assert.That(console.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void TypedPolicyFailure_WritesOrderedImportChainForHumanOutput()
    {
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml", "architecture/fragment.yml", ArchitecturePolicyDocumentRole.Fragment,
            1, "architecture/root.yml", "fragment.yml", ["architecture/root.yml", "architecture/fragment.yml"]);
        var exception = new ArchitecturePolicyLoadException(
            "Policy source file not found: architecture/fragment.yml",
            new ArchitecturePolicyDiagnostic(
                ArchitecturePolicyDiagnosticKind.ImportResolution,
                new ArchitecturePolicySourceLocation(source, "imports[0]", 2, 1, null, null),
                [],
                source.ImportChain),
            ArchitecturePolicyImportErrorCategory.MissingFile.ToString());
        var runtime = new ExplainStubRuntime { ThrowException = exception };
        var console = new RecordingCliConsole();

        int result = Handler(runtime, console).Execute(Options());

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText,
                Does.Contain("Import chain: architecture/root.yml -> architecture/fragment.yml"));
            Assert.That(console.ErrorText, Does.Contain("architecture/fragment.yml"));
        });
    }

    [Test]
    public void RuntimeException_ReturnsErrorWithMessage()
    {
        var runtime = new ExplainStubRuntime { ThrowException = new InvalidOperationException("engine error") };
        var console = new RecordingCliConsole();
        int result = Handler(runtime, console).Execute(Options());

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Does.Contain("Explain error: engine error"));
    }

    // ── Request forwarding ────────────────────────────────────────────────────

    [Test]
    public void Execute_ForwardsSourceTargetModeConditionSetToRuntime()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("NS.A", "NS.B", ["NS.A", "NS.B"], ["contract-x"])
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(
            source: "NS.A", target: "NS.B", mode: "audit", conditionSet: "ci"));

        Assert.Multiple(() =>
        {
            Assert.That(runtime.LastRequest, Is.Not.Null);
            Assert.That(runtime.LastRequest!.Source, Is.EqualTo("NS.A"));
            Assert.That(runtime.LastRequest.Target, Is.EqualTo("NS.B"));
            Assert.That(runtime.LastRequest.Mode, Is.EqualTo("audit"));
            Assert.That(runtime.LastRequest.ConditionSetName, Is.EqualTo("ci"));
        });
    }

    [Test]
    public void Execute_TypeLevel_ParsedAndForwardedToRuntime()
    {
        var runtime = new ExplainStubRuntime();
        Handler(runtime, new RecordingCliConsole()).Execute(Options(level: "type"));

        Assert.That(runtime.LastRequest!.Level, Is.EqualTo(ArchitectureGraphLevel.Type));
    }

    // ── Human format — no path ────────────────────────────────────────────────

    [Test]
    public void Human_NoPath_PrintsNoDependencyMessage()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("NS.A", "NS.B", null, Array.Empty<string>())
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.That(console.OutputText, Does.Contain("No dependency path found"));
        Assert.That(console.OutputText, Does.Contain("NS.A").And.Contain("NS.B"));
    }

    // ── Human format — path without CEL ──────────────────────────────────────

    [Test]
    public void Human_NoPath_PrintsOptionalEmptyCoverage()
    {
        var summary = new ArchitectureCoverageSummary(
            "coverage", "coverage-id", "rule_input",
            new ArchitectureCoverageSummaryCounts(0, 0, 0, 0, 0) { OptionalEmpty = 1 },
            [], [], [], [], [])
        {
            OptionalEmptyItems =
            [
                new ArchitectureCoverageSummaryOptionalEmptyItem(
                    "rule:forbidden:future", "Future module is planned.", "future")
            ]
        };
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("NS.A", "NS.B", null, Array.Empty<string>())
            {
                CoverageSummaries = new[] { summary }
            }
        };
        var console = new RecordingCliConsole();

        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(console.OutputText, Does.Contain("No dependency path found"));
            Assert.That(console.OutputText, Does.Contain("Optional empty input: rule:forbidden:future"));
            Assert.That(console.OutputText, Does.Contain("Future module is planned."));
        });
    }

    [Test]
    public void Human_PathNoCel_PrintsPathAndContractIds()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "C", ["A", "B", "C"], ["rule-1"])
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.That(console.OutputText, Does.Contain("A -> B -> C"));
        Assert.That(console.OutputText, Does.Contain("rule-1"));
        Assert.That(console.OutputText, Does.Not.Contain("when:"));
    }

    [Test]
    public void Human_PathNoContractIds_PrintsPathWithoutContractLine()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], Array.Empty<string>())
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.That(console.OutputText, Does.Contain("A -> B"));
        Assert.That(console.OutputText, Does.Not.Contain("Contract IDs"));
    }

    // ── Human format — CEL expression participation ───────────────────────────

    [Test]
    public void Human_CelMatched_PrintsHopPrefixAndMatchedResult()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule-cel"])
            {
                ExpressionParticipation =
                [
                    new ExplainExpressionParticipation("rule-cel", "target.domain != source.domain", null, ExpressionParticipationResult.Matched)
                    {
                        HopSource = "A",
                        HopTarget = "B",
                    },
                ]
            }
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(console.OutputText, Does.Contain("[rule-cel]"));
            Assert.That(console.OutputText, Does.Contain("A -> B:"));
            Assert.That(console.OutputText, Does.Contain("target.domain != source.domain"));
            Assert.That(console.OutputText, Does.Contain("matched"));
            Assert.That(console.OutputText, Does.Not.Contain("not matched"));
        });
    }

    [Test]
    public void Human_CelNotMatched_PrintsNotMatchedResult()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule-cel"])
            {
                ExpressionParticipation =
                [
                    new ExplainExpressionParticipation("rule-cel", "target.domain != source.domain", null, ExpressionParticipationResult.NotMatched)
                    {
                        HopSource = "A",
                        HopTarget = "B",
                    },
                ]
            }
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.That(console.OutputText, Does.Contain("not matched"));
    }

    [Test]
    public void Human_CelEvaluationFailed_PrintsEvaluationFailedResult()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule-cel"])
            {
                ExpressionParticipation =
                [
                    new ExplainExpressionParticipation("rule-cel", "subject.sourcePaths.exists(p, p.contains(\"/Domain\"))", null, ExpressionParticipationResult.EvaluationFailed)
                    {
                        HopSource = "A",
                        HopTarget = "B",
                    },
                ]
            }
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.That(console.OutputText, Does.Contain("evaluation failed"));
    }

    [Test]
    public void Human_CelWithoutHopInfo_PrintsNoHopPrefix()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule-cel"])
            {
                ExpressionParticipation =
                [
                    new ExplainExpressionParticipation("rule-cel", "target.domain != source.domain", null, ExpressionParticipationResult.Matched),
                ]
            }
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.That(console.OutputText, Does.Contain("[rule-cel]"));
        Assert.That(console.OutputText, Does.Not.Contain("->:"));
    }

    [Test]
    public void Human_MultipleCelEntries_AllPrinted()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "C", ["A", "B", "C"], ["rule-a", "rule-b"])
            {
                ExpressionParticipation =
                [
                    new ExplainExpressionParticipation("rule-a", "expr.one", null, ExpressionParticipationResult.Matched)
                    { HopSource = "A", HopTarget = "B" },
                    new ExplainExpressionParticipation("rule-b", "expr.two", "policy.yml#L10", ExpressionParticipationResult.NotMatched)
                    { HopSource = "B", HopTarget = "C" },
                ]
            }
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(console.OutputText, Does.Contain("[rule-a]").And.Contain("expr.one").And.Contain("matched"));
            Assert.That(console.OutputText, Does.Contain("[rule-b]").And.Contain("expr.two").And.Contain("not matched"));
            Assert.That(console.OutputText, Does.Contain("B -> C:"));
        });
    }

    // ── JSON format — no path ─────────────────────────────────────────────────

    [Test]
    public void Json_NoPath_EmitsNullPath()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("NS.A", "NS.B", null, Array.Empty<string>())
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument doc = JsonDocument.Parse(console.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("source").GetString(), Is.EqualTo("NS.A"));
            Assert.That(doc.RootElement.GetProperty("target").GetString(), Is.EqualTo("NS.B"));
            Assert.That(doc.RootElement.GetProperty("path").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(doc.RootElement.TryGetProperty("expressionParticipation", out _), Is.False);
        });
    }

    [Test]
    public void Json_OptionalEmptyCoverage_EmitsTypedIdentity()
    {
        var summary = new ArchitectureCoverageSummary(
            "coverage", "coverage-id", "rule_input",
            new ArchitectureCoverageSummaryCounts(0, 0, 0, 0, 0) { OptionalEmpty = 1 },
            [], [], [], [], [])
        {
            OptionalEmptyItems =
            [
                new ArchitectureCoverageSummaryOptionalEmptyItem(
                    "rule:forbidden:future", "Future module is planned.", "future")
                {
                    ContractId = "rule",
                    Input = "forbidden",
                    Layer = "future"
                }
            ]
        };
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule"])
            {
                CoverageSummaries = new[] { summary }
            }
        };
        var console = new RecordingCliConsole();

        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        JsonElement item = document.RootElement.GetProperty("coverageSummary")[0]
            .GetProperty("optionalEmptyItems")[0];
        Assert.Multiple(() =>
        {
            Assert.That(item.GetProperty("contractId").GetString(), Is.EqualTo("rule"));
            Assert.That(item.GetProperty("input").GetString(), Is.EqualTo("forbidden"));
            Assert.That(item.GetProperty("layer").GetString(), Is.EqualTo("future"));
        });
    }


    // ── JSON format — path, no CEL ────────────────────────────────────────────

    [Test]
    public void Json_PathNoCel_OmitsExpressionParticipationKey()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule-1"])
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument doc = JsonDocument.Parse(console.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("path").EnumerateArray().Select(e => e.GetString()).ToArray(),
                Is.EqualTo(_value));
            Assert.That(doc.RootElement.GetProperty("contractIds")[0].GetString(), Is.EqualTo("rule-1"));
            Assert.That(doc.RootElement.TryGetProperty("expressionParticipation", out _), Is.False,
                "expressionParticipation must be absent when ExpressionParticipation is empty");
        });
    }

    // ── JSON format — CEL participation ───────────────────────────────────────

    [Test]
    public void Json_CelMatched_EmitsExpressionParticipationArray()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule-cel"])
            {
                ExpressionParticipation =
                [
                    new ExplainExpressionParticipation("rule-cel", "target.domain != source.domain", "policy.yml#L5", ExpressionParticipationResult.Matched)
                    {
                        HopSource = "A",
                        HopTarget = "B",
                    },
                ]
            }
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument doc = JsonDocument.Parse(console.OutputText);
        JsonElement array = doc.RootElement.GetProperty("expressionParticipation");
        JsonElement entry = array[0];

        Assert.Multiple(() =>
        {
            Assert.That(array.GetArrayLength(), Is.EqualTo(1));
            Assert.That(entry.GetProperty("contractId").GetString(), Is.EqualTo("rule-cel"));
            Assert.That(entry.GetProperty("hopSource").GetString(), Is.EqualTo("A"));
            Assert.That(entry.GetProperty("hopTarget").GetString(), Is.EqualTo("B"));
            Assert.That(entry.GetProperty("source").GetString(), Is.EqualTo("target.domain != source.domain"));
            Assert.That(entry.GetProperty("yamlPath").GetString(), Is.EqualTo("policy.yml#L5"));
            Assert.That(entry.GetProperty("result").GetString(), Is.EqualTo("matched"));
        });
    }

    [TestCase(ExpressionParticipationResult.Matched, "matched")]
    [TestCase(ExpressionParticipationResult.NotMatched, "not_matched")]
    [TestCase(ExpressionParticipationResult.EvaluationFailed, "evaluation_failed")]
    public void Json_CelResultValues_SerializeToExpectedStrings(ExpressionParticipationResult result, string expectedString)
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule-cel"])
            {
                ExpressionParticipation =
                [
                    new ExplainExpressionParticipation("rule-cel", "some.expr", null, result),
                ]
            }
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument doc = JsonDocument.Parse(console.OutputText);
        Assert.That(doc.RootElement.GetProperty("expressionParticipation")[0].GetProperty("result").GetString(),
            Is.EqualTo(expectedString));
    }

    [Test]
    public void Json_CelNullYamlPath_SerializesAsNull()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "B", ["A", "B"], ["rule-cel"])
            {
                ExpressionParticipation =
                [
                    new ExplainExpressionParticipation("rule-cel", "expr", null, ExpressionParticipationResult.Matched),
                ]
            }
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument doc = JsonDocument.Parse(console.OutputText);
        Assert.That(doc.RootElement.GetProperty("expressionParticipation")[0].GetProperty("yamlPath").ValueKind,
            Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void Json_MultipleCelEntries_AllIncluded()
    {
        var runtime = new ExplainStubRuntime
        {
            Outcome = new ArchitectureExplainOutcome("A", "C", ["A", "B", "C"], ["rule-a", "rule-b"])
            {
                ExpressionParticipation =
                [
                    new ExplainExpressionParticipation("rule-a", "expr.one", null, ExpressionParticipationResult.Matched)
                    { HopSource = "A", HopTarget = "B" },
                    new ExplainExpressionParticipation("rule-b", "expr.two", null, ExpressionParticipationResult.NotMatched)
                    { HopSource = "B", HopTarget = "C" },
                ]
            }
        };
        var console = new RecordingCliConsole();
        Handler(runtime, console).Execute(Options(format: "json"));

        using JsonDocument doc = JsonDocument.Parse(console.OutputText);
        JsonElement array = doc.RootElement.GetProperty("expressionParticipation");
        Assert.Multiple(() =>
        {
            Assert.That(array.GetArrayLength(), Is.EqualTo(2));
            Assert.That(array[0].GetProperty("contractId").GetString(), Is.EqualTo("rule-a"));
            Assert.That(array[1].GetProperty("contractId").GetString(), Is.EqualTo("rule-b"));
            Assert.That(array[1].GetProperty("result").GetString(), Is.EqualTo("not_matched"));
        });
    }

    // ── Test doubles ──────────────────────────────────────────────────────────

}
