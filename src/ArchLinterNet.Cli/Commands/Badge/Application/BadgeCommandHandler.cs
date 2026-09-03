using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed class BadgeCommandHandler(ICliConsole console, IFileSystem fileSystem)
{
    private const string ArchitectureHealthHelp =
        "arch-linter-net badge architecture-health --input <architecture-health.json> [--output <badge.json>]";

    public int Execute(BadgeCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine("arch-linter-net badge architecture-policy --input <architecture-strict.json>");
            return CliExitCodes.Success;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(fileSystem.ReadAllText(options.InputPath));
            JsonElement result = SelectStrictResult(document.RootElement);
            if (!result.TryGetProperty("passed", out JsonElement value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new JsonException("The strict validation result has no Boolean passed value.");
            }

            bool passed = value.ValueKind == JsonValueKind.True;
            Write(passed ? "passing" : "failing", passed ? "brightgreen" : "red");
            return passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
        }
        catch (Exception)
        {
            Write("unavailable", "red");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    public int ExecuteArchitectureHealth(ArchitectureHealthBadgeCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShowHelp)
        {
            console.Out.WriteLine(ArchitectureHealthHelp);
            return CliExitCodes.Success;
        }

        ArchitectureHealthBadgeProjection projection;
        try
        {
            projection = ArchitectureHealthBadgeProjector.Project(fileSystem.ReadAllText(options.InputPath));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            projection = ArchitectureHealthBadgeProjector.Unassessable();
        }

        try
        {
            Write(projection, options.OutputPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            console.Error.WriteLine($"Could not write Architecture Health badge: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return projection.ExitCode;
    }

    private void Write(string message, string color) => console.Out.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        label = "architecture policy",
        message,
        color,
    }));

    private void Write(ArchitectureHealthBadgeProjection projection, string? outputPath)
    {
        string json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            label = "architecture",
            message = projection.Message,
            color = projection.Color,
        });
        if (outputPath is null)
        {
            console.Out.WriteLine(json);
            return;
        }

        string temporaryPath = fileSystem.WriteAllTextToTemp(outputPath, json + Environment.NewLine);
        fileSystem.RenameTempToTarget(temporaryPath, outputPath);
    }

    private static JsonElement SelectStrictResult(JsonElement document)
    {
        if (!document.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
        {
            if (!document.TryGetProperty("mode", out JsonElement mode) || mode.GetString() != "strict")
            {
                throw new JsonException("The input is not a strict validation result.");
            }

            return document;
        }
        foreach (JsonElement result in results.EnumerateArray()) if (result.TryGetProperty("mode", out JsonElement mode) && mode.GetString() == "strict") return result;
        throw new JsonException("The input does not contain a strict validation result.");
    }
}
