using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class VerifyBaselineSubcommandModule : IBaselineSubcommandModule
{
    public string CommandName => "verify";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        BaselineVerifyCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(CommandName);
        Option<string> policyOption = BaselineOptionsFactory.CreatePolicyOption();
        Option<string> baselineOption = new("--baseline");
        Option<string> modeOption = BaselineOptionsFactory.CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> jsonOption = new("--json");
        Option<string> formatOption = new("--format");
        Option<bool> ensureBuiltOption = new("--ensure-built");
        Option<bool> noRestoreOption = new("--no-restore");
        Option<string> configurationOption = new("--configuration");
        Option<string> targetFrameworkOption = new("--framework");
        Option<string> platformOption = new("--platform");
        Option<string> runtimeIdentifierOption = new("--runtime");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(baselineOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(jsonOption);
        command.Options.Add(formatOption);
        command.Options.Add(ensureBuiltOption);
        command.Options.Add(noRestoreOption);
        command.Options.Add(configurationOption);
        command.Options.Add(targetFrameworkOption);
        command.Options.Add(platformOption);
        command.Options.Add(runtimeIdentifierOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new BaselineVerifyCommandOptions(
            BaselineOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(jsonOption) ? "json" : parseResult.GetValue(formatOption) ?? "human",
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption),
            parseResult.GetValue(ensureBuiltOption),
            parseResult.GetValue(noRestoreOption),
            parseResult.GetValue(configurationOption),
            parseResult.GetValue(targetFrameworkOption),
            parseResult.GetValue(platformOption),
            parseResult.GetValue(runtimeIdentifierOption))
        {
            HasFormatConflict = parseResult.GetValue(jsonOption) && parseResult.GetValue(formatOption) is not null,
        }));

        return command;
    }

}
