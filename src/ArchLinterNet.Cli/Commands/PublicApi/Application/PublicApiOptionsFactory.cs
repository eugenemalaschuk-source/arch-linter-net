using System.CommandLine;
using ParseResult = System.CommandLine.ParseResult;

namespace ArchLinterNet.Cli.Commands.PublicApi.Application;

internal static class PublicApiOptionsFactory
{
    public const string DefaultPolicyPath = "architecture/dependencies.arch.yml";

    public const string HumanFormat = "human";
    public const string JsonFormat = "json";

    // Only `diff` produces a pure finding set, which is the one shape SARIF can represent. capture,
    // update, and migrate report an operation outcome (status, destination, proposed content) that
    // has no SARIF equivalent, so they reject `sarif` instead of silently emitting human text.
    public static readonly IReadOnlyList<string> SupportedFormats = new[] { HumanFormat, JsonFormat, "sarif" };

    public static readonly IReadOnlyList<string> OperationFormats = new[] { HumanFormat, JsonFormat };

    public static Option<string> CreatePolicyOption()
    {
        Option<string> option = new("--policy");
        option.DefaultValueFactory = _ => DefaultPolicyPath;
        option.Aliases.Add("--config");
        return option;
    }

    public static Option<string> CreateContractOption()
    {
        return new Option<string>("--contract");
    }

    public static Option<string> CreateFormatOption()
    {
        Option<string> option = new("--format");
        option.DefaultValueFactory = _ => HumanFormat;
        option.Aliases.Add("-f");
        return option;
    }

    public static Option<bool> CreateHelpOption()
    {
        Option<bool> option = new("--help");
        option.Aliases.Add("-h");
        return option;
    }

    public static Option<bool> CreateEnsureBuiltOption() => new("--ensure-built");

    public static Option<bool> CreateNoRestoreOption() => new("--no-restore");

    public static string GetPolicyPath(ParseResult parseResult, Option<string> policyOption)
    {
        return parseResult.GetValue(policyOption) ?? DefaultPolicyPath;
    }

    public static string GetFormat(ParseResult parseResult, Option<string> formatOption)
    {
        return parseResult.GetValue(formatOption) ?? HumanFormat;
    }
}
