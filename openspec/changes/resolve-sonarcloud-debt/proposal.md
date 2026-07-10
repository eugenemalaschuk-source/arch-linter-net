## Why

SonarCloud quality gate reports 348 open code smells (14h estimated debt) across the codebase, all CODE_SMELL — no bugs, vulnerabilities, or security hotspots. The new-code leak period consistently flags maintainability issues on every PR, creating noise and slowing review. Resolving them eliminates the quality-gate drag and shifts the baseline to a cleaner state.

## What Changes

Fix all 348 open SonarCloud code smells across C# and PowerShell sources, grouped by rule. Work is strictly refactoring — no behavioral changes, no API breaks, no new features.

- **CA1861** (186 issues): Replace repeated array allocations with `static readonly` fields
- **CA1822** (44 issues): Mark instance methods as `static` where they don't access instance state
- **S8677** (26 issues): Fix PowerShell script findings in CI/build tooling
- **S3267** (13 issues): Simplify manual loops to LINQ `Where`/`Select` equivalents
- **CA1859** (9 issues): Narrow return types to concrete types where possible
- **S3011** (8 issues): Review and refactor `BindingFlags.NonPublic` reflection usage
- **S1192** (7 issues): Extract repeated string literals into constants
- **S107** (6 issues): Reduce method/constructor parameter counts exceeding 7
- **S1172** (6 issues): Remove unused method parameters
- **S108** (6 issues): Fill or remove empty code blocks
- **SYSLIB1045** (5 issues): Replace `new Regex(...)` with `[GeneratedRegex]` source generators
- **S2325** (6 issues): Make nested/private methods `static`
- **CA1806** (3 issues): Use return values of method calls
- **CA1865/CA1860/CA1866** (7 issues): Prefer `string` overloads with explicit comparison
- **S127** (3 issues): Fix loop variable mutation inside loop body
- **S3358** (2 issues): Simplify nested ternary expressions
- **S3220** (2 issues): Fix ambiguous `params` overload calls
- **S1144** (2 issues): Remove unused private methods

## Capabilities

### New Capabilities

None. This is a pure refactoring effort — no new capabilities, no behavioral changes.

### Modified Capabilities

None. Fixes are implementation-only; no requirement-level spec changes.

## Impact

- **Core, CLI, Testing, Unity packages**: C# files across all packages touched. Risk is low (mechanical refactoring) but broad.
- **CI/build scripts**: 26 PowerShell issues in `.github/workflows/` and `tools/scripts/`.
- **No API/contract changes**: Public signatures preserved. Some `S107` fixes may introduce parameter objects but no breaking changes.
- **No dependency changes**.
- **Quality gate**: After completion, new-code analysis starts from a clean baseline.
