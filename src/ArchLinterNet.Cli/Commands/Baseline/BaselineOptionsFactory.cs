using System.CommandLine;
using ParseResult = System.CommandLine.ParseResult;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal static class BaselineOptionsFactory
{
    public static Option<string> CreatePolicyOption()
    {
        Option<string> option = new("--policy");
        option.DefaultValueFactory = _ => "architecture/dependencies.arch.yml";
        option.Aliases.Add("--config");
        return option;
    }

    public static Option<string> CreateModeOption()
    {
        Option<string> option = new("--mode");
        option.DefaultValueFactory = _ => "all";
        option.Aliases.Add("-m");
        return option;
    }

    public static string GetPolicyPath(ParseResult parseResult, Option<string> policyOption)
    {
        return parseResult.GetValue(policyOption) ?? "architecture/dependencies.arch.yml";
    }
}
