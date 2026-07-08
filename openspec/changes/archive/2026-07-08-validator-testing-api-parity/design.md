## Context

`ArchLinterNet.Core.Validation.ValidationRequest`/`ValidationOutcome`, reached through the composed `ArchLinterNet.Core.Composition.ArchitectureEngine`, already model every functional choice the CLI's `validate` command exposes: mode (`strict`/`audit`), `ContractIds`, `ConditionSetName`, `BaselinePath`, `EnforceUnmatchedIgnoredViolationsPolicy`, and (via a side-channel `ValidationTiming` parameter to `Engine.Validate`) timings. The CLI already builds a `ValidationRequest` and calls `engine.Validate(request, timing)` directly (`shared-validation-service` spec). The two public convenience surfaces — `ArchitectureValidator.Validate(...)` (3 overloads, Core) and `ArchitectureValidationBuilder`/`ArchitectureAssertions` (Testing) — predate most of that request surface and only thread a handful of fields through, dropping contract selection, baseline merge, unmatched-ignore enforcement, coverage data, and timings entirely.

## Goals / Non-Goals

**Goals:**
- Let programmatic/test callers select the same functional validation modes as CLI callers (contract IDs, condition set, baseline merge, unmatched-ignore enforcement, timings) without hand-composing an `ArchitectureEngineBuilder`.
- Let programmatic/test callers inspect the same structured diagnostics CLI JSON output carries (violations, cycles, coverage findings/summaries, unmatched-ignored violations, policy-consistency findings, pass/fail) without parsing stdout.
- Preserve full backward compatibility for `ArchitectureValidator.Validate(...)`'s three existing overloads and for `ArchitectureValidationBuilder.WithConditionSet`/`ValidateStrict`/`ValidateAudit`.

**Non-Goals:**
- Baseline *lifecycle* operations (generate/update/prune/diff/verify, from #63) do not get new Core/Testing convenience wrappers in this change — only baseline-*merge*-into-a-validation-run (`BaselinePath`), matching what `validate --baseline` itself does. Lifecycle operations stay reachable via `ArchitectureEngine` for integrators who need them.
- No changes to YAML policy schema, CLI behavior, or `ArchitectureEngine`/`ValidationRequest`/`ValidationOutcome` shapes — those already carry everything needed; this change only makes them reachable from the two convenience surfaces.
- No new result/diagnostic types — reuse `ValidationOutcome`'s existing member types (`ArchitectureViolation`, `ArchitectureUnmatchedIgnoredViolation`, `PolicyConsistencyDiagnostic`, `ArchitectureCoverageSummary`) rather than inventing parallel ones.

## Decisions

**1. `ArchitectureValidator` gets one new overload, not a builder.**
`ValidationRequest` is already the "options object" the issue asks for. Adding `ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)` is a direct passthrough to the existing `_engine.Value.Validate(request, timing)` call already used internally — no new abstraction, and it returns the outcome unmodified (not folded into a flat violations list like the legacy overloads do), giving callers full access to every diagnostic category. Alternative considered: a fluent builder mirroring `ArchitectureValidationBuilder` in Core too — rejected as duplicate machinery; `ArchitectureValidator` is a thinner, non-test-framework entry point where an options-record overload is more idiomatic and there's already a Testing-side builder for the fluent style.

**2. `ArchitectureValidationBuilder` grows fluent methods that map 1:1 onto `ValidationRequest` fields.**
`WithContracts(IEnumerable<string>)` / `WithContracts(params string[])`, `WithBaseline(string)`, `WithUnmatchedIgnoredViolationsPolicy(bool enforce = true)`, `WithTimings()`. Each just sets a private field consumed when `Validate(mode)` builds its `ValidationRequest`, following the exact pattern `WithConditionSet` already uses. `WithTimings()` allocates a `ValidationTiming` instance that gets passed to `_engine.Value.Validate(request, timing)` and surfaced on the result, so callers can inspect or write a report themselves — no new timing-formatting logic needed (`ValidationTiming.WriteReport` already exists).

**3. No "all" mode added to the Testing builder or the new `ArchitectureValidator` overload.**
The underlying `ArchitectureValidationApplicationService.Validate` only accepts `"strict"`/`"audit"` (throws `ArgumentException` otherwise) — the CLI's `validate` command itself is restricted the same way; `"all"` mode is a baseline/graph/explain-only concept. `ValidateStrict()`/`ValidateAudit()` remain the only two entry points, and callers using the new `ArchitectureValidator.Validate(ValidationRequest)` overload set `Mode` directly and get the same `ArgumentException` behavior as the CLI for invalid values.

**4. `ArchitectureValidationResult` is extended with additive optional constructor parameters, not restructured.**
New properties (`CoverageFindings`, `CoverageConfig`, `UnmatchedIgnoredViolations`, `UnmatchedIgnoredViolationsConfig`, `CoverageSummaries`, `Timing`) are added as optional trailing constructor parameters with defaults matching today's implicit behavior (empty collections, `"error"`/`"off"` configs consistent with existing `PolicyConsistencyConfig` default, `Timing = null`). This keeps any existing direct construction of the type compiling unchanged while giving the builder a way to populate the full shape.

**5. `ShouldPass()` failure messages grow two more sections, reusing existing formatters.**
`ArchitectureDiagnosticFormatter.FormatCoverageForHumans` and `FormatUnmatchedForHumans` already exist and are already used by the CLI's human-output path. `ShouldPass()` appends their output when `CoverageFindings`/`UnmatchedIgnoredViolations` are non-empty, mirroring how it already appends cycle and policy-consistency detail. This is additive to the message text only — it does not change `Passed`/exception-throwing semantics, which continue to depend solely on `ValidationOutcome.Passed` as computed by the shared service.

## Risks / Trade-offs

- **[Risk]** Expanding `ArchitectureValidationResult`'s constructor could be seen as a breaking API change for any external code constructing it directly (not through the builder). → **Mitigation**: all new parameters are optional with defaults; existing positional-argument call sites (5 args) continue to compile and behave identically.
- **[Risk]** Silent scope-narrowing: readers of issue #64 might expect baseline lifecycle methods (generate/update/prune/diff/verify) on the Testing builder. → **Mitigation**: explicitly documented as a scope decision in proposal.md and this design, distinguishing "baseline merge into validate" (in scope) from "baseline lifecycle commands" (out of scope, already served by #63's CLI subcommands and `ArchitectureEngine`).
- **[Risk]** Timing collection adds a stateful object (`ValidationTiming`) to the builder/result API surface, which is a mutable class rather than an immutable record like the rest of the request/outcome types. → **Mitigation**: this exactly matches the existing `ArchitectureEngine.Validate(request, timing)` signature already used by the CLI, so no new pattern is introduced — callers who don't call `WithTimings()` see `Timing == null` and pay no cost.

## Migration Plan

Purely additive; no migration steps required. No existing call sites change behavior.
