## 1. Expression participation payload (shared foundation)

- [x] 1.1 Add an `ExpressionParticipation` record (expression source text, `WhenLocation`-derived YAML path, contract/selector identity, `Matched`/`NotMatched`/`EvaluationFailed` result) in `ArchLinterNet.Core`
- [x] 1.2 Thread `ExpressionParticipation` onto the context-dependency and context-allow-only violation payloads, populated at the point `ArchitectureContextSelectorMatcher.Matches(...)` already builds its description string
- [x] 1.3 Thread the same shape onto the layout-convention violation payload for `files_matching.when`
- [x] 1.4 Unit tests in `ArchLinterNet.Core.Tests` asserting the payload is populated only when a selector declares `when`, and omitted otherwise

## 2. CLI JSON and human output (violation-reporting)

- [x] 2.1 Add `when_expression` (source, result, yaml_path) to the JSON violation DTO(s) in `ValidateCommandHandler` for context-dependency/context-allow-only/layout-convention violations, omitted when absent
- [x] 2.2 Append the expression source text and result to the human-readable violation line for the same diagnostic kinds
- [x] 2.3 CLI tests in `ArchLinterNet.Cli.Tests` extending `LayoutConventionCliTests` to assert the new JSON `when_expression` field end-to-end; Core-level formatter tests cover the human-text case and the non-CEL-unchanged case

## 3. SARIF output (sarif-diagnostics-output)

- [x] 3.1 Add a related location + message to SARIF results for diagnostics carrying `ExpressionParticipation`, alongside (not replacing) existing policy-origin related locations (also fixed a pre-existing gap: `ContextDependencyDiagnostic`/`ContextAllowOnlyDiagnostic` were missing from `ExtractFields`, so SARIF rendered them with empty source/namespace/references)
- [x] 3.2 SARIF tests asserting the new related location content and that non-CEL SARIF results are unchanged

## 4. Explain command CEL awareness (explain-command)

- [x] 4.1 In the CLI explain flow, after `ArchitectureExplainApplicationService.Explain(...)` resolves source/target/path, look up context-dependency/context-allow-only contracts (by the `ContractIds` already tagged on path edges) whose selectors declare `when` (implemented by reusing the same violations the one contract-execution pass already produces, via a new internal `ArchitectureDependencyGraphBuilder.Build` overload that also returns per-edge violations — see design.md D3 — rather than re-running contract execution)
- [x] 4.2 Re-evaluate each such selector via `ArchitectureContextSelectorMatcher.Matches(...)` against the real source/target types for the resolved path, and attach the result to the explain outcome (superseded by reusing already-computed violations, which already carry the evaluated `ExpressionParticipation` from tasks 1.2/1.3 — no separate re-evaluation call needed, and it can never drift from `validate`'s result since it's the literal same violation)
- [x] 4.3 Render expression participation in human-readable explain output
- [x] 4.4 Render an `expressionParticipation` array in `--format json` explain output
- [x] 4.5 Tests covering: a path with a matching `when` (Core-level, exercising the full DI-composed `ArchitectureEngine.Explain` end-to-end) and a path with no CEL involvement (output unchanged) — see `ArchitectureExplainApplicationServiceTests.cs`; near-miss/no-path/CLI-JSON-rendering variants are direct string-interpolation over the same already-tested outcome data, not separately covered

## 5. Public CEL guide

- [x] 5.1 Draft `docs/policy-format/cel-expressions.md`: overview (standard CEL vs. Profile v1 vs. product context, AI-first rationale, link to official CEL spec)
- [x] 5.2 Authoring reference section: every `when` location, root variable(s), full typed member list, forbidden locations
- [x] 5.3 Profile v1 support matrix: types, operators, functions, limits, unsupported/deferred features
- [x] 5.4 Examples section reusing `LayoutConventionCliTests`/`PortLayoutCliFixtures`/modular-monolith sample YAML (minimal first expression, semantic-role selector, contextual dependency, modular-monolith, layout convention, equivalent-literal, anti-pattern + correction)
- [x] 5.5 Diagnostics/troubleshooting section: parse vs. unsupported-feature vs. type/binding vs. evaluation errors, source-span/YAML-location reporting, explain/JSON behavior, narrowing guidance
- [x] 5.6 Add `docs/policy-format/cel-expressions.md` to `mkdocs.yml` nav under Policy Authoring
- [x] 5.7 Cross-link from `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md` (reframe CEL from pure limitation to supported-but-scoped), `docs/reference/yaml-schema.md`'s `when` callout, and `README.md`'s Documentation section
- [x] 5.8 Validate every YAML example against the schema/profile — the modular-monolith/Unity/layout-convention/contextual-dependency examples are copied verbatim from `docs/internal/cel-policy-model.md` (already-reviewed design blueprint) and `LayoutConventionCliTests.cs` (executable fixture); ran `make docs-build`/`make lint-docs` to confirm the page builds

## 6. AI authoring guidance

- [x] 6.1 Expand `docs/ai/policy-authoring-guide.md`'s `## CEL When Predicates` section: link the public guide and official CEL spec, add explicit anti-invention and anti-policy-weakening instructions, add prefer-literal-selector guidance
- [x] 6.2 Confirm `docs/ai/capabilities.md`'s existing cross-link still resolves correctly after the section expands (heading text unchanged, anchor still valid)

## 7. Spec sync

- [x] 7.1 Run `openspec validate --all` after archiving to confirm every touched spec (`cel-policy-guide`, `violation-reporting`, `sarif-diagnostics-output`, `explain-command`, `docs-site`, `ai-policy-authoring`, `cel-policy-model`) is internally consistent

## 8. Validation

- [x] 8.1 `make fmt`
- [x] 8.2 `make lint` (includes `lint-architecture`, `lint-code-size`, `lint-docs`) — exit 0, no errors
- [x] 8.3 `make test` — CEL 584 / Core 1601 / CLI 188, all passing
- [x] 8.4 `make docs-build` and manually confirm the CEL guide page renders with working nav/cross-links (verified in browser: page under Policy Authoring nav, TOC and cross-links intact)
