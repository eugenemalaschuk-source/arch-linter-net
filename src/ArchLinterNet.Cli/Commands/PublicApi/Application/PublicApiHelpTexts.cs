namespace ArchLinterNet.Cli.Commands.PublicApi.Application;

internal static class PublicApiHelpTexts
{
    public const string HelpText = """
        Usage: arch-linter-net public-api <subcommand> [options]

        Capture, review, and enforce a public API surface as a reviewed snapshot file instead of a
        hand-maintained inline 'declared_api' list.

        Subcommands:
          capture   Write the current exported surface of a contract to a snapshot file.
          diff      Compare a snapshot file against the live exported surface.
          update    Rewrite a snapshot file from the live surface (supports --dry-run).
          migrate   Convert an inline 'declared_api' list into a snapshot file.

        Common options:
          --policy <path>          Policy file (default: architecture/dependencies.arch.yml).
          --contract <id>          Required. Id of a strict/audit public API surface contract.
          --condition-set <name>   Condition set to resolve preprocessor symbols with.
          --format <fmt>           human (default) or json. `diff` also accepts sarif.
          --ensure-built           Build the selected project graph, create a receipt, and
                                   then capture the verified live surface.
          --no-restore             Fail closed when restore is required; combine with
                                   --ensure-built for an offline preparation attempt.
          -h, --help               Show help.

        Paths are repository-local: relative, non-rooted, and inside the policy boundary (the
        policy's directory, or its parent when the policy lives in an `architecture/` folder). The
        policy file itself is never a valid snapshot destination.

        Exit codes:
          0  success / snapshot in sync
          1  a completed gate found drift (diff drift, or unaccepted migrate drift)
          2  invalid arguments, unusable snapshot, blocked build state, or runtime error

        A normal `dotnet build` does not create an ArchLinterNet receipt. To create the first
        reviewed snapshot, run `public-api capture` with `--ensure-built`; subsequent diff and
        update commands reuse the verified receipt-backed artifacts until their inputs change.

        Run 'arch-linter-net public-api <subcommand> --help' for subcommand details.
        """;

    public const string CaptureHelpText = """
        Usage: arch-linter-net public-api capture --contract <id> --output <path> [options]

        Captures the exported (public/protected/protected internal) surface of the contract's target
        assemblies and writes a deterministic snapshot file. Capturing the same surface twice
        produces byte-identical output.

        Options:
          --output <path>   Required. Repository-local snapshot path to write.
          --force           Replace an existing snapshot whose content differs.

        The snapshot records constant and enum values, accessor shape, static/ref/out/in, sealed and
        abstract state, enum underlying type, and generic constraints in addition to the signature,
        so a changed constant value or a widened property is a visible diff rather than a no-op.

        Capture refuses to overwrite an existing, differing snapshot without --force: a snapshot is
        a reviewed artifact, and replacing one silently would hide an unreviewed surface change.

        Add --ensure-built to prepare the selected project graph and create the receipt required
        for capture. Add --no-restore to fail closed instead of restoring during that preparation.
        """;

    public const string DiffHelpText = """
        Usage: arch-linter-net public-api diff --contract <id> --snapshot <path> [options]

        Compares a snapshot file against the live exported surface and reports additions, removals,
        and changed signatures as separate deltas. A member whose signature changed is reported once
        as a change, carrying both the previous and the current signature.

        Options:
          --snapshot <path>   Required. Repository-local snapshot path to compare against.

        Returns exit code 1 when any drift is detected.

        Add --ensure-built to prepare the selected project graph and create the receipt required
        for diff. Add --no-restore to fail closed instead of restoring during that preparation.
        """;

    public const string UpdateHelpText = """
        Usage: arch-linter-net public-api update --contract <id> --snapshot <path> [options]

        Rewrites a snapshot file from the live exported surface and reports the applied delta.
        Entries that did not change are written exactly as before, so the file diff contains only
        the lines that actually moved.

        Options:
          --snapshot <path>     Required. Must resolve to the contract's own `api_snapshot`.
          --dry-run, --check    Report the delta and print the proposed file content without writing.

        Updating a contract that declares its surface inline via 'declared_api' is refused: a YAML
        round-trip cannot preserve the surrounding policy comments. Use 'public-api migrate' first.

        Add --ensure-built to prepare the selected project graph and create the receipt required
        for update. Add --no-restore to fail closed instead of restoring during that preparation.
        """;

    public const string MigrateHelpText = """
        Usage: arch-linter-net public-api migrate --contract <id> --output <path> [options]

        Converts a contract's inline 'declared_api' list into a snapshot file.

        Options:
          --output <path>       Required. Repository-local snapshot path to write.
          --accept-drift        Record the live surface even though it differs from the inline list.
          --force                Replace an existing destination file whose content differs.
          --dry-run, --check    Report the drift without writing.

        Migration refuses to write while the inline list differs from the live surface, so a
        migration can never silently bless surface that was never reviewed. Every stale inline entry
        and undeclared exported member is listed, whether or not drift is accepted.

        Like capture, migrate never overwrites an existing, differing destination without --force: a
        snapshot is a reviewed artifact, and migrate could otherwise silently destroy another
        contract's reviewed snapshot.

        Add --ensure-built to prepare the selected project graph and create the receipt required
        for migration. Add --no-restore to fail closed instead of restoring during that preparation.
        """;
}
