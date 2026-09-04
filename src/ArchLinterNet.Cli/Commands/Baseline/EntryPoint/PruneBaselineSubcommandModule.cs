using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline.EntryPoint;

internal sealed class PruneBaselineSubcommandModule : IBaselineSubcommandModule
{
    public string CommandName => "prune";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        BaselinePruneCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(CommandName);
        Option<string> policyOption = BaselineOptionsFactory.CreatePolicyOption();
        Option<string> baselineOption = new("--baseline");
        Option<string> outputOption = new("--output");
        Option<string> modeOption = BaselineOptionsFactory.CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> jsonOption = new("--json");
        Option<bool> shortFormatOption = new("-f");
        BaselineOptionsFactory.WriteOptionSet writeOptions = BaselineOptionsFactory.CreateWriteOptions();
        BaselineOptionsFactory.BuildStateOptionSet buildStateOptions = BaselineOptionsFactory.CreateBuildStateOptions();
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(baselineOption);
        command.Options.Add(outputOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(jsonOption);
        command.Options.Add(shortFormatOption);
        BaselineOptionsFactory.AddTo(command, writeOptions);
        BaselineOptionsFactory.AddTo(command, buildStateOptions);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new BaselinePruneCommandOptions(
            BaselineOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(jsonOption) || parseResult.GetValue(shortFormatOption) ? "json" : "human",
            BaselineOptionsFactory.Read(parseResult, writeOptions),
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption),
            parseResult.GetValue(buildStateOptions.EnsureBuilt),
            parseResult.GetValue(buildStateOptions.NoRestore),
            parseResult.GetValue(buildStateOptions.Configuration),
            parseResult.GetValue(buildStateOptions.TargetFramework),
            parseResult.GetValue(buildStateOptions.Platform),
            parseResult.GetValue(buildStateOptions.RuntimeIdentifier))));

        return command;
    }
}
