using System.Text.Json;
using ArchLinterNet.Cli.Commands.Coverage.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class CoverageReportRendererTests
{
    [Test]
    public void Renderer_RendersSummaryFailuresAndDiffUnavailable()
    {
        const string json = """
            {"passed":false,"violations":[{"contract_id":"layers","contract":"Layer rules","message_code":"ARCH001","source":"A","subject":"B"}],"coverage_findings":[],"cycle_diagnostics":[],"unmatched_ignored_violations":[],"policy_consistency_findings":[],"preflight_diagnostics":[],"classification_conflicts":[],"classification_metadata_failures":[],"coverage_summary":[{"scope":"namespace","counts":{"covered":2,"excluded":1,"uncovered":3,"stale":4,"unknown":5},"uncovered_items":[],"stale_items":[],"unknown_items":[]}]}
            """;
        using JsonDocument document = JsonDocument.Parse(json);

        string markdown = CoverageReportRenderer.Render(document.RootElement, null, ".", diffFailed: true, maxFailures: 1);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("**Status:** ❌ fail"));
            Assert.That(markdown, Does.Contain("### Failed rules (1)"));
            Assert.That(markdown, Does.Contain("| Uncovered | 3 |"));
            Assert.That(markdown, Does.Contain("### New-code coverage"));
            Assert.That(markdown, Does.Contain("**Unavailable:** the changed-files diff"));
        });
    }

    [Test]
    public void Renderer_ClassifiesChangedFileAndLimitsRepresentativeDiagnostics()
    {
        const string json = """
            {"passed":false,"violations":[{"contract_id":"layers","message_code":"A"},{"contract_id":"layers","message_code":"B"}],"coverage_findings":[],"cycle_diagnostics":[],"unmatched_ignored_violations":[],"policy_consistency_findings":[],"preflight_diagnostics":[],"classification_conflicts":[],"classification_metadata_failures":[],"coverage_summary":[{"scope":"namespace","counts":{},"covered_items":[{"item":"ArchLinterNet.Cli.Commands.Badge.EntryPoint"}],"excluded_items":[],"uncovered_items":[],"stale_items":[],"unknown_items":[]}]}
            """;
        using JsonDocument document = JsonDocument.Parse(json);
        string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));

        string markdown = CoverageReportRenderer.Render(document.RootElement,
            ["src/ArchLinterNet.Cli/Commands/Badge/EntryPoint/BadgeCommandModule.cs"], root, diffFailed: false, maxFailures: 1);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("| Changed namespaces/projects/assemblies covered | 1 |"));
            Assert.That(markdown, Does.Contain("_1 additional diagnostic omitted._"));
        });
    }
}
