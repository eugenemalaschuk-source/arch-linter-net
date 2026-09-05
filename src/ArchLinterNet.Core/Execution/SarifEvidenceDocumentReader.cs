using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

/// <summary>Parses bounded SARIF bytes and exposes typed document, run, and execution facts.</summary>
internal sealed class SarifEvidenceDocumentReader
{
    private const string SupportedFormat = "sarif";
    private const string SupportedVersion = "2.1.0";

    internal static void ValidateRequirement(ArchitectureExternalEvidenceRequirement requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement.Id))
        {
            throw new ArgumentException("An external evidence requirement id is required.", nameof(requirement));
        }

        if (!string.Equals(requirement.Format, SupportedFormat, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The bounded evidence reader supports only SARIF format.", nameof(requirement));
        }

        if (string.IsNullOrWhiteSpace(requirement.Tool))
        {
            throw new ArgumentException("An external evidence tool name is required.", nameof(requirement));
        }

        if (requirement.ToolVersion is not null && string.IsNullOrWhiteSpace(requirement.ToolVersion))
        {
            throw new ArgumentException(
                "An external evidence tool version must be non-blank when supplied.",
                nameof(requirement));
        }

        if (string.IsNullOrWhiteSpace(requirement.Run))
        {
            throw new ArgumentException("An external evidence run id is required.", nameof(requirement));
        }
    }

    internal static bool TryParseDocument(byte[] bytes, out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 128,
                });
            return true;
        }
        catch (JsonException)
        {
            document = null;
            return false;
        }
    }

    internal static bool TryGetRuns(
        JsonElement root,
        out JsonElement runs,
        out SarifEvidenceDocumentFailure? failure,
        out string detail)
    {
        runs = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            failure = SarifEvidenceDocumentFailure.UnsupportedShape;
            detail = "The SARIF document root must be an object.";
            return false;
        }

        if (!root.TryGetProperty("version", out JsonElement version))
        {
            failure = SarifEvidenceDocumentFailure.UnsupportedVersion;
            detail = "The SARIF document does not declare a version.";
            return false;
        }

        if (version.ValueKind != JsonValueKind.String)
        {
            failure = SarifEvidenceDocumentFailure.UnsupportedShape;
            detail = "The SARIF version must be a string.";
            return false;
        }

        if (!string.Equals(version.GetString(), SupportedVersion, StringComparison.Ordinal))
        {
            failure = SarifEvidenceDocumentFailure.UnsupportedVersion;
            detail = "Only SARIF version 2.1.0 is supported.";
            return false;
        }

        if (!root.TryGetProperty("runs", out runs) || runs.ValueKind != JsonValueKind.Array)
        {
            failure = SarifEvidenceDocumentFailure.UnsupportedShape;
            detail = "The SARIF document must contain a runs array.";
            return false;
        }

        failure = null;
        detail = string.Empty;
        return true;
    }

    internal static SarifRunSelection SelectMatchingRun(
        JsonElement runs,
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceLimits limits,
        CancellationToken cancellationToken)
    {
        List<SarifRunCandidate> matches = [];
        int runCount = 0;
        foreach (JsonElement run in runs.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++runCount > limits.MaxRuns)
            {
                return SarifRunSelection.FromFailure(
                    SarifEvidenceDocumentFailure.TooManyRuns,
                    "The SARIF document exceeds the configured run limit.");
            }

            if (!TryGetMatchingCandidate(run, requirement, out SarifRunCandidate? candidate, out SarifRunSelection? failure))
            {
                if (failure is not null)
                {
                    return failure.Value;
                }

                continue;
            }

            matches.Add(candidate!.Value);
        }

        return matches.Count switch
        {
            0 => SarifRunSelection.FromFailure(
                SarifEvidenceDocumentFailure.MissingExpectedRun,
                "No SARIF run matched the configured tool and automation run identity."),
            1 => SarifRunSelection.Selected(matches[0]),
            _ => SarifRunSelection.FromFailure(
                SarifEvidenceDocumentFailure.AmbiguousExpectedRun,
                "More than one SARIF run matched the configured tool and automation run identity.",
                matches[0]),
        };
    }

    internal static int? ReadResultCount(
        JsonElement run,
        SarifEvidenceLimits limits,
        out SarifEvidenceDocumentFailure? failure,
        out string? detail)
    {
        failure = null;
        detail = null;
        if (!run.TryGetProperty("results", out JsonElement results))
        {
            return 0;
        }

        if (results.ValueKind != JsonValueKind.Array)
        {
            failure = SarifEvidenceDocumentFailure.UnsupportedShape;
            detail = "The SARIF run results member must be an array when present.";
            return null;
        }

        int count = 0;
        foreach (JsonElement _ in results.EnumerateArray())
        {
            count++;
            if (count > limits.MaxResults)
            {
                failure = SarifEvidenceDocumentFailure.TooManyResults;
                detail = "The selected SARIF run exceeds the configured result limit.";
                return null;
            }
        }

        return count;
    }

    internal static void ReadExecutionState(
        JsonElement run,
        out SarifEvidenceDocumentFailure? failure,
        out string? detail)
    {
        failure = null;
        detail = null;
        if (!run.TryGetProperty("invocations", out JsonElement invocations))
        {
            failure = SarifEvidenceDocumentFailure.IncompleteExecution;
            detail = "The selected SARIF run does not declare invocation success metadata.";
            return;
        }

        if (invocations.ValueKind != JsonValueKind.Array)
        {
            failure = SarifEvidenceDocumentFailure.UnsupportedShape;
            detail = "The SARIF run invocations member must be an array.";
            return;
        }

        int count = 0;
        foreach (JsonElement invocation in invocations.EnumerateArray())
        {
            count++;
            if (invocation.ValueKind != JsonValueKind.Object
                || !invocation.TryGetProperty("executionSuccessful", out JsonElement successful)
                || successful.ValueKind != JsonValueKind.True && successful.ValueKind != JsonValueKind.False)
            {
                failure = SarifEvidenceDocumentFailure.IncompleteExecution;
                detail = "Every SARIF invocation must explicitly declare a boolean executionSuccessful value.";
                return;
            }

            if (!successful.GetBoolean())
            {
                failure = SarifEvidenceDocumentFailure.FailedExecution;
                detail = "The selected SARIF run contains an unsuccessful invocation.";
                return;
            }
        }

        if (count == 0)
        {
            failure = SarifEvidenceDocumentFailure.IncompleteExecution;
            detail = "The selected SARIF run must contain at least one successful invocation.";
        }
    }

    internal static bool HasDuplicateProperties(JsonElement element, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value, cancellationToken))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasDuplicateProperties(item, cancellationToken))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetMatchingCandidate(
        JsonElement run,
        ArchitectureExternalEvidenceRequirement requirement,
        out SarifRunCandidate? candidate,
        out SarifRunSelection? failure)
    {
        candidate = null;
        failure = null;
        if (run.ValueKind != JsonValueKind.Object)
        {
            failure = SarifRunSelection.FromFailure(
                SarifEvidenceDocumentFailure.UnsupportedShape,
                "Every SARIF run must be an object.");
            return false;
        }

        if (!TryReadRunIdentity(run, out SarifRunCandidate parsed, out string? shapeError))
        {
            if (shapeError is not null)
            {
                failure = SarifRunSelection.FromFailure(SarifEvidenceDocumentFailure.UnsupportedShape, shapeError);
            }

            return false;
        }

        if (!MatchesRequirement(parsed, requirement))
        {
            return false;
        }

        candidate = parsed;
        return true;
    }

    private static bool MatchesRequirement(
        SarifRunCandidate candidate,
        ArchitectureExternalEvidenceRequirement requirement)
    {
        return string.Equals(candidate.ToolName, requirement.Tool, StringComparison.Ordinal)
            && string.Equals(candidate.RunId, requirement.Run, StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(requirement.ToolVersion)
                || string.Equals(candidate.ToolVersion, requirement.ToolVersion, StringComparison.Ordinal));
    }

    private static bool TryReadRunIdentity(
        JsonElement run,
        out SarifRunCandidate candidate,
        out string? shapeError)
    {
        candidate = default;
        shapeError = null;

        if (!run.TryGetProperty("tool", out JsonElement tool))
        {
            return false;
        }

        if (tool.ValueKind != JsonValueKind.Object)
        {
            shapeError = "The SARIF run tool must be an object.";
            return false;
        }

        if (!tool.TryGetProperty("driver", out JsonElement driver))
        {
            return false;
        }

        if (driver.ValueKind != JsonValueKind.Object)
        {
            shapeError = "The SARIF run tool driver must be an object.";
            return false;
        }

        if (!driver.TryGetProperty("name", out JsonElement name))
        {
            return false;
        }

        if (name.ValueKind != JsonValueKind.String)
        {
            shapeError = "The SARIF tool driver name must be a string.";
            return false;
        }

        string? toolVersion = null;
        if (driver.TryGetProperty("version", out JsonElement version))
        {
            if (version.ValueKind != JsonValueKind.String)
            {
                shapeError = "The SARIF tool driver version must be a string.";
                return false;
            }

            toolVersion = version.GetString();
        }

        if (!run.TryGetProperty("automationDetails", out JsonElement automationDetails))
        {
            return false;
        }

        if (automationDetails.ValueKind != JsonValueKind.Object)
        {
            shapeError = "The SARIF run automationDetails must be an object.";
            return false;
        }

        if (!automationDetails.TryGetProperty("id", out JsonElement runId))
        {
            return false;
        }

        if (runId.ValueKind != JsonValueKind.String)
        {
            shapeError = "The SARIF automationDetails id must be a string.";
            return false;
        }

        candidate = new SarifRunCandidate(
            run,
            name.GetString() ?? string.Empty,
            toolVersion,
            runId.GetString() ?? string.Empty);
        return true;
    }
}

internal readonly record struct SarifRunCandidate(
    JsonElement Run,
    string ToolName,
    string? ToolVersion,
    string RunId);

internal readonly record struct SarifRunSelection(
    SarifRunCandidate? Candidate,
    SarifEvidenceDocumentFailure? Failure,
    string? Detail)
{
    public static SarifRunSelection Selected(SarifRunCandidate candidate) => new(candidate, null, null);

    public static SarifRunSelection FromFailure(
        SarifEvidenceDocumentFailure failure,
        string detail,
        SarifRunCandidate? candidate = null) => new(candidate, failure, detail);
}

internal enum SarifEvidenceDocumentFailure
{
    UnsupportedVersion,
    UnsupportedShape,
    MissingExpectedRun,
    AmbiguousExpectedRun,
    FailedExecution,
    IncompleteExecution,
    TooManyRuns,
    TooManyResults,
}
