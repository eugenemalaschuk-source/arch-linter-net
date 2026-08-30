using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed partial class SarifEvidenceReader
{
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

    private static bool HasDuplicateProperties(JsonElement element, CancellationToken cancellationToken)
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

    private static int? ReadResultCount(
        JsonElement run,
        SarifEvidenceLimits limits,
        out SarifEvidenceTrustStatus? status,
        out string? detail)
    {
        status = null;
        detail = null;
        if (!run.TryGetProperty("results", out JsonElement results))
        {
            return 0;
        }

        if (results.ValueKind != JsonValueKind.Array)
        {
            status = SarifEvidenceTrustStatus.UnsupportedShape;
            detail = "The SARIF run results member must be an array when present.";
            return null;
        }

        int count = 0;
        foreach (JsonElement _ in results.EnumerateArray())
        {
            count++;
            if (count > limits.MaxResults)
            {
                status = SarifEvidenceTrustStatus.TooManyResults;
                detail = "The selected SARIF run exceeds the configured result limit.";
                return null;
            }
        }

        return count;
    }

    private static ExecutionState ReadExecutionState(
        JsonElement run,
        out SarifEvidenceTrustStatus? status,
        out string? detail)
    {
        status = null;
        detail = null;
        if (!run.TryGetProperty("invocations", out JsonElement invocations))
        {
            status = SarifEvidenceTrustStatus.IncompleteExecution;
            detail = "The selected SARIF run does not declare invocation success metadata.";
            return default;
        }

        if (invocations.ValueKind != JsonValueKind.Array)
        {
            status = SarifEvidenceTrustStatus.UnsupportedShape;
            detail = "The SARIF run invocations member must be an array.";
            return default;
        }

        int count = 0;
        foreach (JsonElement invocation in invocations.EnumerateArray())
        {
            count++;
            if (invocation.ValueKind != JsonValueKind.Object
                || !invocation.TryGetProperty("executionSuccessful", out JsonElement successful)
                || successful.ValueKind != JsonValueKind.True && successful.ValueKind != JsonValueKind.False)
            {
                status = SarifEvidenceTrustStatus.IncompleteExecution;
                detail = "Every SARIF invocation must explicitly declare a boolean executionSuccessful value.";
                return default;
            }

            if (!successful.GetBoolean())
            {
                status = SarifEvidenceTrustStatus.FailedExecution;
                detail = "The selected SARIF run contains an unsuccessful invocation.";
                return default;
            }
        }

        if (count == 0)
        {
            status = SarifEvidenceTrustStatus.IncompleteExecution;
            detail = "The selected SARIF run must contain at least one successful invocation.";
        }

        return default;
    }

    private readonly record struct SarifRunCandidate(
        JsonElement Run,
        string ToolName,
        string? ToolVersion,
        string RunId);

    private readonly record struct ExecutionState;
}
