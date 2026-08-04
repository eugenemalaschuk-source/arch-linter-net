using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Caching;

namespace ArchLinterNet.Cli.Commands.Cache;

// `cache inspect`/`cache clear` — see openspec/specs/analysis-cache/spec.md, "Inspect and clear
// operations are safe and deterministic".
internal sealed class CacheCommandHandler(ICliConsole console)
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public int ShowHelp()
    {
        console.Out.WriteLine(CacheCommandDefinition.HelpText);
        return CliExitCodes.Success;
    }

    public int Inspect(string? cacheDestination, bool showHelp)
    {
        if (showHelp)
        {
            return ShowHelp();
        }

        if (!TryResolveLocation(cacheDestination, out AnalysisCacheLocation? location))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        IReadOnlyList<AnalysisCacheEntrySummary> summaries = AnalysisCacheStore.Inspect(location!);
        console.Out.WriteLine(JsonSerializer.Serialize(summaries, _jsonOptions));
        return CliExitCodes.Success;
    }

    public int Clear(string? cacheDestination, bool showHelp)
    {
        if (showHelp)
        {
            return ShowHelp();
        }

        if (!TryResolveLocation(cacheDestination, out AnalysisCacheLocation? location))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            AnalysisCacheStore.Clear(location!);
        }
        catch (AnalysisCacheLocationRejectedException ex)
        {
            console.Error.WriteLine($"Cannot clear cache: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        console.Out.WriteLine(JsonSerializer.Serialize(new { cleared = true }, _jsonOptions));
        return CliExitCodes.Success;
    }

    private bool TryResolveLocation(string? cacheDestination, out AnalysisCacheLocation? location)
    {
        location = null;
        if (string.IsNullOrWhiteSpace(cacheDestination))
        {
            console.Error.WriteLine("--cache <auto|path> is required.");
            return false;
        }

        AnalysisCacheOptions options = cacheDestination == "auto"
            ? AnalysisCacheOptions.Auto
            : AnalysisCacheOptions.AtPath(cacheDestination);

        try
        {
            location = AnalysisCacheLocationResolver.Resolve(options);
            return true;
        }
        catch (AnalysisCacheLocationRejectedException ex)
        {
            console.Error.WriteLine($"Cannot use --cache '{cacheDestination}': {ex.Message}");
            return false;
        }
    }
}
