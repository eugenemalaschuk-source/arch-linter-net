namespace ArchLinterNet.Cli.Commands.Baseline;

internal static class BaselineHelpTexts
{
    public const string HelpText =
        """
        arch-linter-net baseline — manage baseline lifecycle commands

        Usage:
          arch-linter-net baseline generate --config <path> --output <path> [options]
          arch-linter-net baseline update --config <path> --baseline <path> --output <path> [options]
          arch-linter-net baseline prune --config <path> --baseline <path> --output <path> [options]
          arch-linter-net baseline diff --config <path> --baseline <path> [options]
          arch-linter-net baseline verify --config <path> --baseline <path> [options]
          arch-linter-net baseline migrate --config <path> --baseline <path> --output <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --output <path>     Path to write the generated baseline file (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --reason <text>     Reason text for baseline entries
                              (default: "generated baseline")
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
                              to control conditional compilation symbols during
                              Roslyn source analysis (default: policy
                              default_condition_set, otherwise empty symbol set)
          -h, --help          Show this help message

        Subcommands:
          generate   Generate a fresh baseline from current violations
          update     Update an existing baseline, preserving valid entries
          prune      Remove stale/resolved entries from an existing baseline
          diff       Report new/existing/resolved/configuration-error entries
          verify     Exit non-zero if the baseline is out of sync (CI gate)
          migrate    Upgrade a legacy version 1 baseline to structured version 2 identity

        Run 'arch-linter-net baseline <subcommand> --help' for subcommand-specific options.

        Exit codes:
          0   Command completed successfully
          1   Baseline verify found resolved entries or configuration errors
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public const string UpdateHelpText =
        """
        arch-linter-net baseline update — update a baseline while preserving valid entries

        Usage:
          arch-linter-net baseline update --config <path> --baseline <path> --output <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --baseline <path>   Path to the existing baseline file to update (required)
          --output <path>     Path to write the updated baseline file (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --reason <text>     Reason text for newly added entries
                              (default: "generated baseline")
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
          -h, --help          Show this help message

        Exit codes:
          0   Baseline updated successfully
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public const string PruneHelpText =
        """
        arch-linter-net baseline prune — remove stale entries from a baseline

        Usage:
          arch-linter-net baseline prune --config <path> --baseline <path> --output <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --baseline <path>   Path to the existing baseline file to prune (required)
          --output <path>     Path to write the pruned baseline file (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
          --json              Report removed entries as JSON
          -h, --help          Show this help message

        Exit codes:
          0   Baseline pruned successfully
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public const string DiffHelpText =
        """
        arch-linter-net baseline diff — compare a baseline against current violations

        Usage:
          arch-linter-net baseline diff --config <path> --baseline <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --baseline <path>   Path to the baseline file to diff against (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
          --json               Output the diff as JSON
          -h, --help          Show this help message

        Exit codes:
          0   Diff produced successfully (regardless of drift found)
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public const string VerifyHelpText =
        """
        arch-linter-net baseline verify — verify a baseline is in sync (CI gate)

        Usage:
          arch-linter-net baseline verify --config <path> --baseline <path> [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --baseline <path>   Path to the baseline file to verify (required)
          --mode <mode>       Contract mode: strict, audit, or all (default: all)
          --contract <id>     Restrict to this contract ID (may be repeated)
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
          --json               Output the verification report as JSON
          -h, --help          Show this help message

        Exit codes:
          0   Baseline is in sync
          1   Baseline is out of sync (resolved entries or configuration errors found)
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;

    public const string MigrateHelpText =
        """
        arch-linter-net baseline migrate — upgrade a legacy version 1 baseline to version 2

        Deterministically rewrites a legacy `(source_type, forbidden_reference)` baseline into
        the structured, versioned identity format. Every entry in the file is correlated
        against current violations — this command always processes the whole file, with no
        --mode/--contract scoping, because a version-2 document cannot preserve version-1
        matching semantics for only part of a file (an unexamined entry could be ambiguous
        under structured identity, discoverable only by correlating it). Exactly one match
        migrates the entry; zero matches reports it as stale (dropped); more than one match
        reports it as ambiguous and the command fails closed — no file is written until
        ambiguous entries are resolved. The source file is never overwritten.

        Usage:
          arch-linter-net baseline migrate --config <path> --baseline <path> --output <path> [options]
          arch-linter-net baseline migrate --config <path> --baseline <path> --dry-run [options]

        Options:
          --policy, --config <path>
                              Path to YAML contract file
                              (default: architecture/dependencies.arch.yml)
          --baseline <path>   Path to the legacy version 1 baseline file to migrate (required)
          --output <path>     Path to write the migrated version 2 baseline file
                              (required unless --dry-run/--check; must differ from --baseline)
          --dry-run, --check  Report classification without writing any file
          --condition-set <name>
                              Use a named condition set from analysis.condition_sets
          --json              Output the migration report as JSON
          -h, --help          Show this help message

        Exit codes:
          0   Migration completed (or dry run reported) with no ambiguous entries
          1   Ambiguous entries found — no file written, manual review required
          2   Runtime error (invalid arguments, file not found, config violations, etc.)
        """;
}
