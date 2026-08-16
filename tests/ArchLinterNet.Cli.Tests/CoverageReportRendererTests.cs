using System.Text.Json;
using ArchLinterNet.Cli.Commands.Coverage.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class CoverageReportRendererTests
{
    [Test]
    public void Renderer_RendersZeroFindingsAsPass()
    {
        const string Json = """{"passed":true,"coverage_summary":[]}""";
        using JsonDocument document = JsonDocument.Parse(Json);

        string markdown = CoverageReportRenderer.Render(document.RootElement, null, ".", false, null);

        Assert.That(markdown, Does.Contain("**Status:** ✅ pass"));
    }

    [Test]
    public void Renderer_RendersSummaryFailuresAndDiffUnavailable()
    {
        const string Json = """
            {"passed":false,"violations":[{"contract_id":"layers","contract":"Layer rules","message_code":"ARCH001","source":"A","subject":"B","forbidden_references":["X.Y"],"policy_origin":{"path":"policy.yml","line":4}}],"coverage_findings":[],"cycle_diagnostics":[],"unmatched_ignored_violations":[],"policy_consistency_findings":[],"preflight_diagnostics":[],"classification_conflicts":[],"classification_metadata_failures":[],"coverage_summary":[{"scope":"namespace","counts":{"covered":2,"excluded":1,"uncovered":3,"stale":4,"unknown":5},"covered_items":[],"excluded_items":[],"uncovered_items":[],"stale_items":[],"unknown_items":[]}]}
            """;
        using JsonDocument document = JsonDocument.Parse(Json);

        string markdown = CoverageReportRenderer.Render(document.RootElement, null, ".", diffFailed: true, maxFailures: 1);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("**Status:** ❌ fail"));
            Assert.That(markdown, Does.Contain("### Failed rules (1)"));
            Assert.That(markdown, Does.Contain("| Uncovered | 3 |"));
            Assert.That(markdown, Does.Contain("### New-code coverage"));
            Assert.That(markdown, Does.Contain("**Unavailable:** the changed-files diff"));
            Assert.That(markdown, Does.Contain("forbidden references `X.Y`"));
            Assert.That(markdown, Does.Contain("policy `policy.yml:4`"));
        });
    }

    [Test]
    public void Renderer_ClassifiesChangedFileAndLimitsRepresentativeDiagnostics()
    {
        const string Json = """
            {"passed":false,"violations":[{"contract_id":"layers","message_code":"A"},{"contract_id":"layers","message_code":"B"}],"coverage_findings":[],"cycle_diagnostics":[],"unmatched_ignored_violations":[],"policy_consistency_findings":[],"preflight_diagnostics":[],"classification_conflicts":[],"classification_metadata_failures":[],"coverage_summary":[{"scope":"namespace","counts":{},"covered_items":[{"item":"ArchLinterNet.Cli.Commands.Badge.EntryPoint"}],"excluded_items":[],"uncovered_items":[],"stale_items":[],"unknown_items":[]}]}
            """;
        using JsonDocument document = JsonDocument.Parse(Json);
        string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));

        string markdown = CoverageReportRenderer.Render(document.RootElement,
            ["src/ArchLinterNet.Cli/Commands/Badge/EntryPoint/BadgeCommandModule.cs"], root, diffFailed: false, maxFailures: 1);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("| Changed namespaces/projects/assemblies covered | 1 |"));
            Assert.That(markdown, Does.Contain("_1 additional diagnostic omitted._"));
        });
    }

    [Test]
    public void Renderer_ReportsUnmappableChangedFileAsUnknown()
    {
        const string Json = """{"passed":true,"coverage_summary":[{"scope":"namespace","counts":{},"covered_items":[],"excluded_items":[],"uncovered_items":[],"stale_items":[],"unknown_items":[]}]}""";
        using JsonDocument document = JsonDocument.Parse(Json);

        string markdown = CoverageReportRenderer.Render(document.RootElement, ["missing-source.cs"], TestContext.CurrentContext.TestDirectory, false, null);

        Assert.That(markdown, Does.Contain("| Requiring policy update | 1 |"));
    }

    private const string EmptyFailureCollections =
        """"
        "coverage_findings":[],"cycle_diagnostics":[],"unmatched_ignored_violations":[],"policy_consistency_findings":[],"preflight_diagnostics":[],"classification_conflicts":[],"classification_metadata_failures":[]
        """";

    [Test]
    public void Renderer_FailureWithoutStructuredDiagnostics_ReportsUnavailable()
    {
        string json = $$"""{"passed":false,"violations":[],{{EmptyFailureCollections}},"coverage_summary":[]}""";
        using JsonDocument document = JsonDocument.Parse(json);

        string markdown = CoverageReportRenderer.Render(document.RootElement, null, ".", false, null);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("### Failed rules (0)"));
            Assert.That(markdown, Does.Contain("**Unavailable:** strict validation failed without structured diagnostics"));
        });
    }

    [Test]
    public void Renderer_OmittedDiagnostics_UsesPluralWording()
    {
        string json = $$"""
            {"passed":false,"violations":[{"contract_id":"layers","message_code":"A"},{"contract_id":"layers","message_code":"B"},{"contract_id":"layers","message_code":"C"}],{{EmptyFailureCollections}},"coverage_summary":[]}
            """;
        using JsonDocument document = JsonDocument.Parse(json);

        string markdown = CoverageReportRenderer.Render(document.RootElement, null, ".", diffFailed: false, maxFailures: 1);

        Assert.That(markdown, Does.Contain("_2 additional diagnostics omitted._"));
    }

    [Test]
    public void Renderer_PreflightDiagnostics_SkipsCurrentStateAndIncludesOthers()
    {
        const string Json = """
            {"passed":false,"violations":[],"coverage_findings":[],"cycle_diagnostics":[],"unmatched_ignored_violations":[],"policy_consistency_findings":[],"preflight_diagnostics":[{"state":"current","message_code":"skip-me"},{"state":"stale","message_code":"include-me"}],"classification_conflicts":[],"classification_metadata_failures":[],"coverage_summary":[]}
            """;
        using JsonDocument document = JsonDocument.Parse(Json);

        string markdown = CoverageReportRenderer.Render(document.RootElement, null, ".", false, null);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Not.Contain("skip-me"));
            Assert.That(markdown, Does.Contain("include-me"));
        });
    }

    [Test]
    public void Renderer_FindingWithoutContractIdentifier_FallsBackToCategorySlug()
    {
        const string Json = """
            {"passed":false,"violations":[],"coverage_findings":[],"cycle_diagnostics":[],"unmatched_ignored_violations":[],"policy_consistency_findings":[],"preflight_diagnostics":[],"classification_conflicts":[{"message_code":"orphan"}],"classification_metadata_failures":[],"coverage_summary":[]}
            """;
        using JsonDocument document = JsonDocument.Parse(Json);

        string markdown = CoverageReportRenderer.Render(document.RootElement, null, ".", false, null);

        Assert.That(markdown, Does.Contain("`classification-conflict`"));
    }

    [Test]
    public void Renderer_ChangedFile_SkipsFixturesAndTestProjects()
    {
        const string Json = """{"passed":true,"coverage_summary":[{"scope":"namespace","counts":{},"covered_items":[],"excluded_items":[],"uncovered_items":[],"stale_items":[],"unknown_items":[]}]}""";
        using JsonDocument document = JsonDocument.Parse(Json);
        string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));

        string markdown = CoverageReportRenderer.Render(document.RootElement,
            ["tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/modular-consumer/src/Synthetic.Modules.M01/Synthetic.Modules.M01.csproj",
             "tests/ArchLinterNet.Cli.Tests/CoverageReportRendererTests.cs"],
            root, false, null);

        Assert.That(markdown, Does.Contain("| Changed first-party files | 2 |"));
        Assert.That(markdown, Does.Not.Contain("Items needing attention:"));
    }

    [Test]
    public void Renderer_ClassifiesProjectAndAssemblyScopesUsingCsprojAssemblyName()
    {
        string root = Path.Combine(Path.GetTempPath(), "archlinternet-renderer-" + Guid.NewGuid().ToString("N"));
        string projectDir = Path.Combine(root, "src", "Widgets");
        Directory.CreateDirectory(projectDir);
        string projectPath = Path.Combine(projectDir, "Widgets.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><AssemblyName>Contoso.Widgets</AssemblyName></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(projectDir, "Widget.cs"), "namespace Contoso.Widgets;\n\npublic class Widget { }\n");

        try
        {
            const string Json = """
                {"passed":true,"coverage_summary":[
                    {"scope":"project","counts":{},"covered_items":[{"item":"src/Widgets/Widgets.csproj"}],"excluded_items":[],"uncovered_items":[],"stale_items":[],"unknown_items":[]},
                    {"scope":"assembly","counts":{},"covered_items":[{"item":"Contoso.Widgets"}],"excluded_items":[],"uncovered_items":[],"stale_items":[],"unknown_items":[]}
                ]}
                """;
            using JsonDocument document = JsonDocument.Parse(Json);

            string markdown = CoverageReportRenderer.Render(document.RootElement, ["src/Widgets/Widget.cs"], root, false, null);

            Assert.That(markdown, Does.Contain("| Changed namespaces/projects/assemblies covered | 2 |"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void Renderer_ProjectWithoutAssemblyNameTag_FallsBackToFileName()
    {
        string root = Path.Combine(Path.GetTempPath(), "archlinternet-renderer-" + Guid.NewGuid().ToString("N"));
        string projectDir = Path.Combine(root, "src", "Widgets");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "Widgets.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(projectDir, "Widget.cs"), "namespace Contoso.Widgets;\n");

        try
        {
            const string Json = """{"passed":true,"coverage_summary":[{"scope":"assembly","counts":{},"covered_items":[{"item":"Widgets"}],"excluded_items":[],"uncovered_items":[],"stale_items":[],"unknown_items":[]}]}""";
            using JsonDocument document = JsonDocument.Parse(Json);

            string markdown = CoverageReportRenderer.Render(document.RootElement, ["src/Widgets/Widget.cs"], root, false, null);

            Assert.That(markdown, Does.Contain("| Changed namespaces/projects/assemblies covered | 1 |"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void Renderer_MixedCoverageStates_CountsStaleAsUncovered()
    {
        const string Json = """
            {"passed":true,"coverage_summary":[{"scope":"namespace","counts":{},"covered_items":[],"excluded_items":[],"uncovered_items":[],"stale_items":[{"item":"ArchLinterNet.Cli.Commands.Badge.EntryPoint"}],"unknown_items":[]}]}
            """;
        using JsonDocument document = JsonDocument.Parse(Json);
        string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
        string file = "src/ArchLinterNet.Cli/Commands/Badge/EntryPoint/BadgeCommandModule.cs";

        string markdown = CoverageReportRenderer.Render(document.RootElement, [file], root, false, null);

        Assert.That(markdown, Does.Contain("| Changed namespaces/projects/assemblies uncovered | 1 |"));
    }
}
