using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Composes the human document from the immutable validation outcome. It does not know about sinks
// or publication; the coordinator owns those concerns.
internal sealed class HumanReportRenderer
{
    private readonly ICliRuntime _runtime;

    public HumanReportRenderer(ICliRuntime runtime)
    {
        _runtime = runtime;
    }

    internal string Render(
        bool isSingleMode,
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken = default)
    {
        return isSingleMode
            ? FormatSingle(outcomesByMode[0].Outcome, cancellationToken)
            : FormatCombined(outcomesByMode, cancellationToken);
    }

    internal static string StripAnsi(string content) => AnsiEscapeSequenceStripper.Strip(content);

    private string FormatSingle(ValidationOutcome outcome, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        AppendSectionSet(sb, outcome, cancellationToken);
        return StripAnsi(sb.ToString().TrimEnd());
    }

    private string FormatCombined(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach ((string mode, ValidationOutcome outcome) in outcomesByMode)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!first)
            {
                sb.AppendLine();
            }

            first = false;
            if (outcomesByMode.Count > 1)
            {
                sb.AppendLine($"=== Mode: {mode} ===");
            }

            AppendSectionSet(sb, outcome, cancellationToken);
        }

        return StripAnsi(sb.ToString().TrimEnd());
    }

    private void AppendSectionSet(
        StringBuilder sb, ValidationOutcome outcome, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string preflight = FormatPreflight(outcome);
        if (!string.IsNullOrEmpty(preflight))
        {
            sb.AppendLine(preflight);
        }

        if (outcome.PreflightBlocked)
        {
            return;
        }

        if (outcome.Passed)
        {
            sb.AppendLine("Architecture validation passed.");
        }
        else
        {
            if (outcome.Violations.Count > 0)
            {
                sb.AppendLine(_runtime.FormatViolationsForHumans(outcome.Violations, cancellationToken));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (outcome.Cycles.Count > 0)
            {
                sb.AppendLine(_runtime.FormatCyclesForHumans(outcome.Cycles, outcome.CycleFindings));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (outcome.ImportedDiagnosticFindings.Count > 0)
        {
            sb.AppendLine(ArchitectureDiagnosticFormatter.FormatFindingsForHumans(
                outcome.ImportedDiagnosticFindings, cancellationToken));
        }

        string assessmentCompletion = outcome.ApplicabilityProjection is { } applicabilityProjection
            ? ArchitectureDiagnosticFormatter.FormatApplicabilityProjectionForHumans(applicabilityProjection)
            : ArchitectureDiagnosticFormatter.FormatAssessmentCompletionForHumans(
                outcome.AssessmentCompletionEvidence);
        if (!string.IsNullOrEmpty(assessmentCompletion))
        {
            sb.AppendLine(assessmentCompletion);
        }

        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.PolicyConsistencyConfig != "off" && outcome.PolicyConsistencyFindings.Count > 0,
            () => _runtime.FormatPolicyConsistencyForHumans(outcome.PolicyConsistencyFindings));
        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.UnmatchedIgnoredViolations.Count > 0 && outcome.UnmatchedIgnoredViolationsConfig != "off",
            () => _runtime.FormatUnmatchedForHumans(outcome.UnmatchedIgnoredViolations));
        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.PolicyInventory is not null,
            () => ArchitectureDiagnosticFormatter.FormatPolicyInventoryForHumans(outcome.PolicyInventory));
        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.Waivers.Count > 0,
            () => _runtime.FormatWaiversForHumans(outcome.Waivers));
        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.CoverageConfig != "off" && outcome.CoverageFindings.Count > 0,
            () => _runtime.FormatCoverageForHumans(outcome.CoverageFindings, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.CoverageSummaries.Count > 0,
            () => _runtime.FormatCoverageSummaryForHumans(outcome.CoverageSummaries));
        cancellationToken.ThrowIfCancellationRequested();
        AppendSection(sb, outcome.ClassificationConflicts.Count > 0 || outcome.ClassificationMetadataFailures.Count > 0
                || outcome.ClassificationPathDeferred != null,
            () => _runtime.FormatClassificationFactsForHumans(
                outcome.ClassificationConflicts, outcome.ClassificationMetadataFailures, outcome.ClassificationPathDeferred));
    }

    private string FormatPreflight(ValidationOutcome outcome)
    {
        if (outcome.PreflightDiagnostics.Count == 0)
        {
            return string.Empty;
        }

        string text = _runtime.FormatBuildStatePreflightForHumans(outcome.PreflightDiagnostics);
        return string.IsNullOrEmpty(text) ? string.Empty : $"\n{text}";
    }

    private static void AppendSection(StringBuilder sb, bool shouldWrite, Func<string> contentFactory)
    {
        if (!shouldWrite)
        {
            return;
        }

        string content = contentFactory();
        if (!string.IsNullOrEmpty(content))
        {
            sb.AppendLine();
            sb.AppendLine(content);
        }
    }
}
