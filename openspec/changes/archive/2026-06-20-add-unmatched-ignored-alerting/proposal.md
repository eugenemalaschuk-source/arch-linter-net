## Why

Stale `ignored_violations` entries accumulate when the underlying dependency debt is resolved but the baseline entry remains. Over time this erodes trust in the architecture policy — it becomes unclear which debt is still present vs. which ignores are dead. Inspired by Python Import Linter's `unmatched_ignore_imports_alerting`.

## What Changes

- **`ArchitectureIgnoreUsageTracker`** — tracks which ignored-violation entries (by index) matched at least one actual violation during contract checking
- **Extended `ArchitectureIgnoreMatcher.IsIgnored`** — accepts an optional tracker parameter; when present, records matched entries without short-circuiting (so all matching entries are tracked, not just the first)
- **`ArchitectureUnmatchedIgnoredViolation`** record — new model type distinct from `ArchitectureViolation`, representing stale baseline entries
- **`analysis.unmatched_ignored_violations`** config field — `error` (default, fails validation) | `warn` (reports but doesn't fail) | `off` (no detection)
- **Modified `Check*Contract` methods** — each creates a per-contract tracker, passes it to all `IsIgnored` calls, then diffs matched indices against `contract.IgnoredViolations` to produce unmatched diagnostics
- **CLI integration** — exit code accounts for unmatched entries at `error` severity; human and JSON formatters emit a separate section/field
- **JSON schema + docs update** — new config field documented, unmatched output format specified
- **Not BREAKING** — existing policies with `ignored_violations` continue to work unchanged; the new config defaults to `error` but is additive

## Capabilities

### New Capabilities
- `unmatched-ignore-alerting`: Detecting and reporting stale `ignored_violations` entries that no longer match any current dependency violation

### Modified Capabilities
- `ignore-matching`: No spec-level requirement changes — matching logic semantics are preserved. The extension to track matched entries is an implementation detail.

## Impact

| Area | Impact |
|------|--------|
| `ArchLinterNet.Core` | New model type, modified matcher, modified runner `Check*Contract` methods, updated finder/scanner signatures (`IReadOnlyCollection` → `IReadOnlyList`) |
| `ArchLinterNet.Cli` | Exit code logic expanded, output format extended for both human and JSON modes |
| `schema/dependencies.arch.schema.json` | New `analysis.unmatched_ignored_violations` field with enum validation |
| `docs/reference/yaml-schema.md` | Document new config field and unmatched output format |
| `docs/ai/` | Update AI-facing guidance |
| Tests | New test fixtures for matched/unmatched/mixed scenarios across contract types |
