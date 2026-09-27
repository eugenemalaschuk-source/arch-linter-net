using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Cli.Commands.Policy.Application;

internal sealed record PolicyWeakeningCommandOptions(
    string BaseContextPath,
    string CurrentContextPath,
    string Format,
    bool ShowHelp,
    string? PublicApiApprovalPath = null,
    string? PolicyPath = null,
    string? ConditionSetName = null,
    BuildPreparationMode PreparationMode = BuildPreparationMode.Ordinary,
    bool NoRestore = false);
