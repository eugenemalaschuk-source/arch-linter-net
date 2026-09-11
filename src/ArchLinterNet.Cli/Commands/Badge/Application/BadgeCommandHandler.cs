using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed class BadgeCommandHandler(ICliConsole console, IFileSystem fileSystem)
{
    private const string ArchitectureHealthHelp =
        "arch-linter-net badge architecture-health --input <architecture-health.json> [--output <badge.json>] "
        + "[--disclosure-profile <headline-only/v1|headline-plus-freshness/v1>] [--verified-at <UTC>] "
        + "[--verify-disclosure-profile]";

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

        if (options.VerifyDisclosureProfile)
        {
            return VerifyDisclosureProfile(options);
        }

        ArchitectureHealthBadgeProjection projection;
        try
        {
            projection = ArchitectureHealthBadgeProjector.Project(
                fileSystem.ReadAllText(options.InputPath),
                options.DisclosureProfile,
                options.VerifiedAt);
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

    private int VerifyDisclosureProfile(ArchitectureHealthBadgeCommandOptions options)
    {
        if (options.OutputPath is not null || options.VerifiedAt is not null || options.DisclosureProfile is null)
        {
            console.Error.WriteLine("Profile verification requires --input and --disclosure-profile only.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            bool valid = ArchitectureHealthBadgeDisclosureValidator.TryValidate(
                options.DisclosureProfile,
                fileSystem.ReadAllBytes(options.InputPath),
                out string digest);
            console.Out.WriteLine(JsonSerializer.Serialize(new { valid, sha256 = valid ? digest : null }));
            return valid ? CliExitCodes.Success : CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            console.Error.WriteLine($"Could not verify Architecture Health badge profile: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
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
        string json = Serialize(projection);
        if (projection.DisclosureProfile is not null
            && !ArchitectureHealthBadgeDisclosureValidator.TryValidate(
                projection.DisclosureProfile,
                Encoding.UTF8.GetBytes(json),
                out _))
        {
            projection = ArchitectureHealthBadgeProjector.Unassessable();
            json = Serialize(projection);
        }

        if (outputPath is null)
        {
            console.Out.WriteLine(json);
            return;
        }

        string temporaryPath = fileSystem.WriteAllTextToTemp(outputPath, json + Environment.NewLine);
        fileSystem.RenameTempToTarget(temporaryPath, outputPath);
    }

    private static string Serialize(ArchitectureHealthBadgeProjection projection) => projection.VerifiedAt is null
            ? JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                label = "architecture",
                message = projection.Message,
                color = projection.Color,
            })
            : JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                label = "architecture",
                message = projection.Message,
                color = projection.Color,
                verified_at = projection.VerifiedAt,
                valid_until = projection.ValidUntil,
            });

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
