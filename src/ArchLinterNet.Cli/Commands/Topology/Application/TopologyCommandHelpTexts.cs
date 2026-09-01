namespace ArchLinterNet.Cli.Commands.Topology.Application;

internal static class TopologyCommandHelpTexts
{
    internal const string Capture =
        """
        arch-linter-net topology capture — capture canonical topology observations for review

        Usage:
          arch-linter-net topology capture --subject-kind <kind> [options]

        Options:
          -p, --policy <path>       Policy path (default: architecture/dependencies.arch.yml)
              --subject-kind <k>   type, namespace, project, or assembly
          -f, --format <fmt>        human or json (default: human)
              --json                Shortcut for --format json
              --output <path>       Write the review artifact to a file
              --condition-set <n>   Named condition set for source analysis
              --ensure-built        Build and verify the selected project graph
              --no-restore          Fail closed instead of restoring
              --configuration <n>   Build configuration
              --framework <tfm>     Target framework
              --platform <name>     Build platform
              --runtime <rid>       Runtime identifier
              --max-parallelism <n> Maximum analysis parallelism
          -h, --help                Show this help message

        Use --ensure-built to create the verified build receipt required for a first capture.
        Later commands can reuse unchanged receipt-backed artifacts; a regular dotnet build alone
        does not create that receipt.
        """;

    internal const string Diff =
        """
        arch-linter-net topology diff — review declared-versus-observed topology evidence

        Usage:
          arch-linter-net topology diff [options]

        Options:
          -p, --policy <path>       Policy path (default: architecture/dependencies.arch.yml)
          -m, --mode <mode>         strict or audit (default: strict)
              --strict              Shortcut for --mode strict
              --audit               Shortcut for --mode audit
          -f, --format <fmt>        human or json (default: human)
              --json                Shortcut for --format json
              --output <path>       Write the review artifact to a file
              --baseline <path>     Baseline path passed to ordinary validation
              --contract <id>       Restrict validation to a contract (repeatable)
              --condition-set <n>   Named condition set for source analysis
              --ensure-built        Build and verify the selected project graph
              --no-restore          Fail closed instead of restoring
              --configuration <n>   Build configuration
              --framework <tfm>     Target framework
              --platform <name>     Build platform
              --runtime <rid>       Runtime identifier
              --max-parallelism <n> Maximum analysis parallelism
          -h, --help                Show this help message
        """;

    internal const string Verify =
        """
        arch-linter-net topology verify — verify declared topology using ordinary validation

        Usage:
          arch-linter-net topology verify [options]

        Options are the same as topology diff. Verify preserves ordinary validation output and
        exit semantics; topology drift is not converted into a second success criterion.
        """;
}
