## Context

`CliHost` retains legacy root help and version rendering for compatibility. It
currently performs that rendering before parsing, so a leading help or version
token prevents later invalid input from being diagnosed.

## Goals / Non-Goals

**Goals:**

- Apply parser error and unmatched-token validation to the entire argument
  vector before any successful legacy help/version response.
- Preserve existing output for valid root help and version invocations.
- Cover both host-level dispatch and process-level CLI behavior.

**Non-Goals:**

- Replace the legacy root help/version renderer.
- Change valid subcommand help behavior or add commands/options.

## Decisions

- Construct and parse the root command before calling the legacy renderer. This
  reuses the command tree as the single authority for input validity and avoids
  a second, incomplete argv scanner.
- If parsing reports an error or unmatched token, emit the existing normalized
  diagnostics and return exit code 2 before considering legacy output. This
  makes token position immaterial.
- Keep the legacy renderer after successful parsing so its established root
  help/version text remains unchanged. Invoking the parser alone would not
  preserve that formatting contract.

## Risks / Trade-offs

- [A test command tree may not model global version support] → Make test
  factories declare the valid root tokens required by the host behavior.
- [Parser behavior differs by command] → Exercise leading help/version followed
  by unknown commands and options through the real CLI integration tests.
