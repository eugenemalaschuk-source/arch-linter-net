## ADDED Requirements

### Requirement: Explain reports CEL expression participation on the resolved path
When the `explain` command resolves a path (or direct edge) whose contract IDs include a context-dependency or context-allow-only contract with a `when`-bearing selector, the command SHALL additionally report, for that selector, the expression's source text and whether it matched (`true`/`false`) for the actual `--source`/`--target` pair being explained, re-evaluated via the same selector-matching logic `validate` uses.

#### Scenario: Human explain output shows expression participation
- **WHEN** `arch-linter-net explain --source A --target B` resolves a path where a hop's contract has a `when`-bearing forbidden selector
- **THEN** the human-readable output includes the expression's source text and whether it matched for that hop

#### Scenario: JSON explain output shows expression participation
- **WHEN** the same explain request is made with `--format json`
- **THEN** the JSON output includes an `expressionParticipation` array with entries containing the contract ID, expression source text, YAML location, and match result

#### Scenario: Explain without CEL involvement is unaffected
- **WHEN** the resolved path involves no `when`-bearing selector on any hop
- **THEN** the explain output (human or JSON) is identical to the output produced before this change

#### Scenario: No-path result has no expression participation
- **WHEN** no path exists between `--source` and `--target`
- **THEN** the "no dependency path found" result is unchanged and includes no expression participation data
