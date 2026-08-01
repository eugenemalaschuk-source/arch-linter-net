using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArchLinterNet.Cli.Commands.Validate;

// Builds one complete error document for a requested report format. Keeping this independent of
// ValidateCommandHandler makes the single-document stream guarantee explicit and keeps command
// dispatch below the repository's file-size limit.
internal static class ReportErrorContentFormatter
{
    public static string BuildOutputErrorJsonText(string status, string message, RouteResult result, string reportJson)
    {
        return JsonSerializer.Serialize(new
        {
            kind = "architecture_execution_error",
            output_status = status,
            message,
            failed_paths = result.FailedPaths,
            committed_paths = result.CommittedPaths,
            uncommitted_paths = result.UncommittedPaths,
            errors = result.ErrorDetails,
            report = JsonNode.Parse(reportJson),
        });
    }

    // Merges the real report's SARIF runs with one additional synthetic run describing the
    // routing failure itself, rather than nesting the report under the failure — a SARIF consumer
    // that only reads top-level runs still sees every real finding, not just a summary of them.
    public static string BuildOutputErrorSarifText(string status, string message, RouteResult result, string reportSarif)
    {
        JsonArray runs = JsonNode.Parse(reportSarif)?["runs"]?.AsArray() is JsonArray reportRuns
            ? new JsonArray(reportRuns.Select(run => run?.DeepClone()).ToArray())
            : new JsonArray();

        runs.Add(new JsonObject
        {
            ["tool"] = new JsonObject { ["driver"] = new JsonObject { ["name"] = "arch-linter-net" } },
            ["results"] = new JsonArray(new JsonObject
            {
                ["ruleId"] = "architecture-output",
                ["message"] = new JsonObject { ["text"] = message },
                ["properties"] = new JsonObject
                {
                    ["output_status"] = status,
                    ["failed_paths"] = ToJsonArray(result.FailedPaths),
                    ["committed_paths"] = ToJsonArray(result.CommittedPaths),
                    ["uncommitted_paths"] = ToJsonArray(result.UncommittedPaths),
                    ["errors"] = ToJsonArray(result.ErrorDetails),
                },
                ["locations"] = new JsonArray(),
            }),
        });

        return new JsonObject { ["version"] = "2.1.0", ["runs"] = runs }.ToJsonString();
    }

    public static string BuildOutputErrorHumanText(string message, RouteResult result, string reportHuman)
    {
        var sb = new System.Text.StringBuilder(message);
        sb.Append('\n').Append(reportHuman);
        if (result.UncommittedPaths.Count > 0)
        {
            sb.Append($"\n  uncommitted: {string.Join(", ", result.UncommittedPaths)}");
        }
        foreach (string detail in result.ErrorDetails)
        {
            sb.Append($"\n  {detail}");
        }
        return sb.ToString();
    }

    // Issue #375: cancellation observed during multi-sink staging/commit (RouteResult.Cancelled)
    // gets its own "cancelled" shape rather than reusing the output_status "partial-output"/
    // "output-failed" literals — a caller distinguishing completion statuses must never see
    // cancellation reported as a generic output failure. Mirrors BuildOutputError*'s structure
    // (same committed/uncommitted/failed evidence, same embedded already-rendered report) exactly.
    public static string BuildCancelledOutputJsonText(string message, RouteResult result, string reportJson)
    {
        return JsonSerializer.Serialize(new
        {
            kind = "architecture_cancelled",
            status = "cancelled",
            message,
            failed_paths = result.FailedPaths,
            committed_paths = result.CommittedPaths,
            uncommitted_paths = result.UncommittedPaths,
            errors = result.ErrorDetails,
            report = JsonNode.Parse(reportJson),
        });
    }

    public static string BuildCancelledOutputSarifText(string message, RouteResult result, string reportSarif)
    {
        JsonArray runs = JsonNode.Parse(reportSarif)?["runs"]?.AsArray() is JsonArray reportRuns
            ? new JsonArray(reportRuns.Select(run => run?.DeepClone()).ToArray())
            : new JsonArray();

        runs.Add(new JsonObject
        {
            ["tool"] = new JsonObject { ["driver"] = new JsonObject { ["name"] = "arch-linter-net" } },
            ["results"] = new JsonArray(new JsonObject
            {
                ["ruleId"] = "architecture-cancelled",
                ["message"] = new JsonObject { ["text"] = message },
                ["properties"] = new JsonObject
                {
                    ["status"] = "cancelled",
                    ["failed_paths"] = ToJsonArray(result.FailedPaths),
                    ["committed_paths"] = ToJsonArray(result.CommittedPaths),
                    ["uncommitted_paths"] = ToJsonArray(result.UncommittedPaths),
                    ["errors"] = ToJsonArray(result.ErrorDetails),
                },
                ["locations"] = new JsonArray(),
            }),
        });

        return new JsonObject { ["version"] = "2.1.0", ["runs"] = runs }.ToJsonString();
    }

    public static string BuildCancelledOutputHumanText(string message, RouteResult result, string reportHuman)
    {
        var sb = new System.Text.StringBuilder(message);
        sb.Append('\n').Append(reportHuman);
        if (result.CommittedPaths.Count > 0)
        {
            sb.Append($"\n  committed: {string.Join(", ", result.CommittedPaths)}");
        }
        if (result.UncommittedPaths.Count > 0)
        {
            sb.Append($"\n  uncommitted: {string.Join(", ", result.UncommittedPaths)}");
        }
        foreach (string detail in result.ErrorDetails)
        {
            sb.Append($"\n  {detail}");
        }
        return sb.ToString();
    }

    public static string BuildErrorRoutingFailureJsonText(
        string status, string originalJson, RouteResult routeResult)
    {
        JsonObject document = JsonNode.Parse(originalJson)?.AsObject().DeepClone().AsObject()
            ?? new JsonObject { ["kind"] = "architecture_execution_error" };
        document["output_status"] = status;
        document["failed_paths"] = ToJsonArray(routeResult.FailedPaths);
        document["committed_paths"] = ToJsonArray(routeResult.CommittedPaths);
        document["uncommitted_paths"] = ToJsonArray(routeResult.UncommittedPaths);
        document["errors"] = ToJsonArray(routeResult.ErrorDetails);
        return document.ToJsonString();
    }

    private static JsonArray ToJsonArray(IReadOnlyList<string> values)
    {
        return new JsonArray(values.Select(value => (JsonNode)value).ToArray());
    }
}
