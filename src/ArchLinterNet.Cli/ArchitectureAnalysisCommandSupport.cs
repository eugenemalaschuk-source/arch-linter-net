using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli;

internal sealed record ArchitectureAnalysisCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string Mode,
    string? ConditionSetName,
    string Format,
    IReadOnlyList<string> ContractIds,
    string? BaseContextPath,
    string? CurrentContextPath,
    bool ShowHelp,
    bool EnsureBuilt,
    bool NoRestore,
    string? Configuration,
    string? TargetFramework,
    string? Platform,
    string? RuntimeIdentifier);

internal sealed class ArchitectureAnalysisCommandOptionSet
{
    public Option<string> Policy { get; } = WithDefault("--policy", "architecture/dependencies.arch.yml");

    public Option<string> Baseline { get; } = new("--baseline");

    public Option<string> Mode { get; } = WithDefault("--mode", "all");

    public Option<string> ConditionSet { get; } = new("--condition-set");

    public Option<string[]> Contracts { get; } = new("--contract")
    {
        AllowMultipleArgumentsPerToken = true,
    };

    public Option<string> BaseContext { get; } = new("--base-context");

    public Option<string> CurrentContext { get; } = new("--current-context");

    public Option<string> Format { get; } = WithDefault("--format", "human");

    public Option<bool> EnsureBuilt { get; } = new("--ensure-built");

    public Option<bool> NoRestore { get; } = new("--no-restore");

    public Option<string> Configuration { get; } = new("--configuration");

    public Option<string> Framework { get; } = new("--framework");

    public Option<string> Platform { get; } = new("--platform");

    public Option<string> Runtime { get; } = new("--runtime");

    public Option<bool> Help { get; } = new("--help");

    public ArchitectureAnalysisCommandOptionSet()
    {
        Policy.Aliases.Add("-p");
        Mode.Aliases.Add("-m");
        Format.Aliases.Add("-f");
        Help.Aliases.Add("-h");
    }

    public void AddTo(Command command)
    {
        command.Options.Add(Policy);
        command.Options.Add(Baseline);
        command.Options.Add(Mode);
        command.Options.Add(ConditionSet);
        command.Options.Add(Contracts);
        command.Options.Add(BaseContext);
        command.Options.Add(CurrentContext);
        command.Options.Add(Format);
        command.Options.Add(EnsureBuilt);
        command.Options.Add(NoRestore);
        command.Options.Add(Configuration);
        command.Options.Add(Framework);
        command.Options.Add(Platform);
        command.Options.Add(Runtime);
        command.Options.Add(Help);
    }

    public ArchitectureAnalysisCommandOptions Read(ParseResult result) => new(
        result.GetValue(Policy) ?? "architecture/dependencies.arch.yml",
        result.GetValue(Baseline),
        result.GetValue(Mode) ?? "all",
        result.GetValue(ConditionSet),
        result.GetValue(Format) ?? "human",
        result.GetValue(Contracts) ?? Array.Empty<string>(),
        result.GetValue(BaseContext),
        result.GetValue(CurrentContext),
        result.GetValue(Help),
        result.GetValue(EnsureBuilt),
        result.GetValue(NoRestore),
        result.GetValue(Configuration),
        result.GetValue(Framework),
        result.GetValue(Platform),
        result.GetValue(Runtime));

    private static Option<string> WithDefault(string name, string value)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => value;
        return option;
    }
}

internal static class ArchitectureAnalysisCommandSupport
{
    public static bool TryValidateContexts(
        ICliConsole console,
        IFileSystem fileSystem,
        ArchitectureAnalysisCommandOptions options)
    {
        string baseContextPath = options.BaseContextPath ?? string.Empty;
        string currentContextPath = options.CurrentContextPath ?? string.Empty;
        bool hasBase = !string.IsNullOrWhiteSpace(baseContextPath);
        bool hasCurrent = !string.IsNullOrWhiteSpace(currentContextPath);
        if (hasBase != hasCurrent)
        {
            CliErrorOutputWriter.Write(console, options.Format, "missing-policy-context", "Both --base-context and --current-context are required together.");
            return false;
        }

        if (hasBase
            && (!fileSystem.FileExists(baseContextPath) || !fileSystem.FileExists(currentContextPath)))
        {
            CliErrorOutputWriter.Write(console, options.Format, "missing-policy-context", "Both policy-context artifact files must exist.");
            return false;
        }

        return true;
    }

    public static ArchitectureDebtGateRequest CreateDebtGateRequest(
        ArchitectureAnalysisCommandOptions options,
        IFileSystem fileSystem,
        CancellationToken cancellationToken) => new()
        {
            PolicyPath = options.PolicyPath,
            BaselinePath = options.BaselinePath ?? string.Empty,
            Mode = options.Mode,
            ConditionSetName = options.ConditionSetName,
            ContractIds = options.ContractIds,
            BasePolicyContext = options.BaseContextPath is null
                ? null
                : ArchitecturePolicyWeakeningFormatter.DeserializeContext(fileSystem.ReadAllText(options.BaseContextPath)),
            CurrentPolicyContext = options.CurrentContextPath is null
                ? null
                : ArchitecturePolicyWeakeningFormatter.DeserializeContext(fileSystem.ReadAllText(options.CurrentContextPath)),
            PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
            NoRestore = options.NoRestore,
            RequestedConfiguration = options.Configuration,
            RequestedTargetFramework = options.TargetFramework,
            RequestedPlatform = options.Platform,
            RequestedRuntimeIdentifier = options.RuntimeIdentifier,
            CancellationToken = cancellationToken,
        };
}
