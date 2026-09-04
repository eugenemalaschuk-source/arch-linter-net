using System.CommandLine;
using ArchLinterNet.Core.Validation;
using ParseResult = System.CommandLine.ParseResult;

namespace ArchLinterNet.Cli.Commands.Baseline.Application;

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

    /// <summary>Reason text plus its per-contract and per-family mappings.</summary>
    internal sealed record ReasonOptionSet(
        Option<string> Reason,
        Option<string[]> ForContract,
        Option<string[]> ForFamily);

    /// <summary>Preview and overwrite-intent options shared by every writing subcommand.</summary>
    internal sealed record WriteOptionSet(Option<bool> DryRun, Option<bool> Force);

    /// <summary>Build-state options shared by every subcommand that runs a live analysis pass.</summary>
    internal sealed record BuildStateOptionSet(
        Option<bool> EnsureBuilt,
        Option<bool> NoRestore,
        Option<string> Configuration,
        Option<string> TargetFramework,
        Option<string> Platform,
        Option<string> RuntimeIdentifier);

    public static ReasonOptionSet CreateReasonOptions()
    {
        Option<string> reason = new("--reason");
        reason.DefaultValueFactory = _ => BaselineReasonMap.DefaultReasonText;
        return new ReasonOptionSet(reason, new Option<string[]>("--reason-for-contract"), new Option<string[]>("--reason-for-family"));
    }

    public static WriteOptionSet CreateWriteOptions()
    {
        return new WriteOptionSet(new Option<bool>("--dry-run"), new Option<bool>("--force"));
    }

    public static BuildStateOptionSet CreateBuildStateOptions()
    {
        return new BuildStateOptionSet(
            new Option<bool>("--ensure-built"),
            new Option<bool>("--no-restore"),
            new Option<string>("--configuration"),
            new Option<string>("--framework"),
            new Option<string>("--platform"),
            new Option<string>("--runtime"));
    }

    public static void AddTo(Command command, ReasonOptionSet reasons)
    {
        command.Options.Add(reasons.Reason);
        command.Options.Add(reasons.ForContract);
        command.Options.Add(reasons.ForFamily);
    }

    public static void AddTo(Command command, WriteOptionSet write)
    {
        command.Options.Add(write.DryRun);
        command.Options.Add(write.Force);
    }

    public static void AddTo(Command command, BuildStateOptionSet buildState)
    {
        command.Options.Add(buildState.EnsureBuilt);
        command.Options.Add(buildState.NoRestore);
        command.Options.Add(buildState.Configuration);
        command.Options.Add(buildState.TargetFramework);
        command.Options.Add(buildState.Platform);
        command.Options.Add(buildState.RuntimeIdentifier);
    }

    public static BaselineReasonOptions Read(ParseResult parseResult, ReasonOptionSet reasons)
    {
        return new BaselineReasonOptions(
            parseResult.GetValue(reasons.Reason) ?? BaselineReasonMap.DefaultReasonText,
            parseResult.GetValue(reasons.ForContract) ?? Array.Empty<string>(),
            parseResult.GetValue(reasons.ForFamily) ?? Array.Empty<string>());
    }

    public static BaselineWriteOptions Read(ParseResult parseResult, WriteOptionSet write)
    {
        return new BaselineWriteOptions(parseResult.GetValue(write.DryRun), parseResult.GetValue(write.Force));
    }
}
