## MODIFIED Requirements

### Requirement: History command family and authored range operands
The shipped CLI SHALL expose a `history` command family whose `analyze`
subcommand runs canonical Release Architecture Forensics analysis and emits the
versioned successful report over an explicit authored range.

`analyze` SHALL accept a required `--from` operand, a required `--to` operand,
an optional `--repository` path defaulting to the current directory, an optional
`--policy` path, and an optional `--format` selector accepting `json` (default)
and `markdown`. `--from` is exclusive and `--to` is inclusive.

The repository SHALL be located by walking from the requested path toward the
filesystem root until a Git directory is found, supporting both a `.git`
directory and a `.git` file containing a `gitdir:` pointer. The repository
object-hash format SHALL be read from the repository's own configuration,
defaulting to SHA-1 when no `extensions.objectformat` value is declared, and an
unrecognized declared format SHALL fail closed.

Authored operands SHALL resolve exactly as `release-architecture-forensics`
specifies: literal `HEAD`, a full lowercase-or-uppercase hexadecimal object ID
whose length matches the repository hash format, a fully-qualified `refs/...`
name, or a shorthand looked up only as `refs/tags/<operand>` and
`refs/heads/<operand>`. Shorthand matching both a tag and a head SHALL fail as
ambiguous. Symbolic refs SHALL be dereferenced with cycle detection, annotated
tags SHALL peel recursively, and a final non-commit object SHALL fail closed.
Revision-expression syntax such as `HEAD~2` SHALL NOT be interpreted.

#### Scenario: Default repository and format
- **WHEN** `history analyze --from <a> --to <b>` runs inside a Git working tree
  without `--repository` or `--format`
- **THEN** the enclosing repository is discovered by upward search and the
  versioned canonical JSON report is emitted

#### Scenario: Markdown report
- **WHEN** `history analyze --from <a> --to <b> --format markdown` succeeds
- **THEN** the deterministic human-readable report is written without changing
  the canonical JSON artifact semantics

#### Scenario: Shorthand collision
- **WHEN** both `refs/tags/release` and `refs/heads/release` exist and `--to release` is authored
- **THEN** the command fails with an ambiguous-ref diagnostic and emits no successful report

#### Scenario: Revision expression rejected
- **WHEN** `--from HEAD~2` is authored and no ref with that exact name exists
- **THEN** the command fails with an unresolved-ref diagnostic instead of evaluating ancestry syntax
