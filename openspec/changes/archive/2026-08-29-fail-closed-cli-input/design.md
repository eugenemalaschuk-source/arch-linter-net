## Context

`CliHost` owns central `System.CommandLine` parsing and maps parser diagnostics to the documented invalid-arguments exit category. By default, the command parser allows unmatched tokens to reach a root or parent command's normal help route, so unknown top-level tokens and subcommands can return success.

## Goals / Non-Goals

**Goals:**

- Treat unmatched top-level tokens and subcommands as invalid CLI input.
- Preserve explicit valid root and subcommand help behavior.
- Keep diagnostics deterministic and consistent with the existing `--help` guidance.

**Non-Goals:**

- Add aliases or guessed commands.
- Redesign the command tree, parser, or help text.
- Change validation, policy, or architecture-evaluation semantics.

## Decisions

### Reject parser-unmatched tokens in `CliHost`

`CliHost` will inspect `ParseResult.UnmatchedTokens` together with parser errors before invocation. This preserves the parser's record of unknown root tokens and nested subcommands even when `--help` would otherwise select successful parent help. The centralized parser-error writer will retain ownership of the token-specific diagnostic, usage guidance, and `CliExitCodes.InvalidArgumentsOrRuntimeError` mapping.

This is preferred over manually walking command tokens, enabling `TreatUnmatchedTokensAsErrors`, or changing individual default handlers: the parser's unmatched-token projection remains available even when explicit help suppresses parser errors. Declared arguments such as `schema print <logical-id>` remain valid, while every unmatched command token fails closed.

### Test behavior through process-invocation integration tests

Regression coverage will invoke the packaged CLI artifact with unknown top-level and nested command tokens, an unrecognised option, and explicit help. This validates the actual process exit code and output streams that CI consumes.

## Risks / Trade-offs

- [Declared positional arguments could be rejected with an unknown subcommand] → Delegate matching to `System.CommandLine`, which knows each command's declared arguments.
- [Existing parent-help routes could become errors] → Cover explicit valid help and retain parser-owned help-option behavior while enabling strict unmatched-token handling.
