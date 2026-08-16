using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed class BadgeCommandHandler(ICliConsole console, IFileSystem fileSystem)
{
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
            bool passed = result.TryGetProperty("passed", out JsonElement value) && value.ValueKind == JsonValueKind.True;
            Write(passed ? "passing" : "failing", passed ? "brightgreen" : "red");
            return passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
        }
        catch (Exception)
        {
            Write("unavailable", "red");
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

    private static JsonElement SelectStrictResult(JsonElement document)
    {
        if (!document.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array) return document;
        foreach (JsonElement result in results.EnumerateArray()) if (result.TryGetProperty("mode", out JsonElement mode) && mode.GetString() == "strict") return result;
        throw new JsonException("The input does not contain a strict validation result.");
    }
}
