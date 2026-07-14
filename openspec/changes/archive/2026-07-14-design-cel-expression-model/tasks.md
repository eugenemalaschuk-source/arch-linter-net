## 1. Design Document

- [x] 1.1 Verify `design.md` covers all acceptance criteria from issue #162: allowed locations, forbidden locations, two input contexts, validation lifecycle split, backward compatibility, and non-goals
- [x] 1.2 Verify `design.md` includes both a modular-monolith example and a Unity/client-style example with complete YAML
- [x] 1.3 Verify `design.md` includes at least one negative example for each of: stale `when` condition, load-time variable-context error, unsupported function call
- [x] 1.4 Confirm all four open questions (matches/RE2, final whitelist, evaluation-time failure behavior, JSON diagnostic schema) are documented with their blocking dependency (#166 or #163)

## 2. Specification

- [x] 2.1 Verify `specs/cel-expression-model/spec.md` has at least one scenario per requirement that is testable as an acceptance fixture
- [x] 2.2 Verify the spec explicitly lists every forbidden YAML field section (classification, reason, name, id, namespace, role, analysis, ignored_violations)
- [x] 2.3 Verify the spec requirement for "no live schema changes until #163" is present and distinguishable from the other requirements
- [x] 2.4 Verify the spec requirement for host-access prohibition covers file system, network, process, and reflection

## 3. Validation Against Existing Policy Fixtures

- [x] 3.1 Manually review `samples/policies/modular-monolith.yml` — confirm no existing field would be misread as a `when` expression, and that all existing `strict_context_dependencies` / `strict_context_allow_only` entries remain valid as-is
- [x] 3.2 Manually review `samples/policies/unity-asmdef-boundaries.yml` — confirm no existing field would be misread as a `when` expression
- [x] 3.3 Manually review `samples/policies/imports/modular-monolith/` fragment files — confirm fragment selectors are unaffected
- [x] 3.4 Manually review `schema/dependencies.arch.schema.json` `selector` and `contextSelector` definitions — confirm `additionalProperties: false` already rejects an unknown `when` field, so existing schema validation already fails closed

## 4. Compatibility Check Against Related Specs

- [x] 4.1 Read `openspec/specs/semantic-classification-model/spec.md` — confirm the `when` design does not conflict with any classification requirement
- [x] 4.2 Read `openspec/specs/contextual-dependency-contracts/spec.md` — confirm the target-position context (`type.*` + `source.*`) is consistent with the four-operator `not-equal-to-source` semantics already specified
- [x] 4.3 Read `openspec/specs/contextual-allow-only-contracts/spec.md` — confirm the `allowed` selector `when` design is consistent with that spec's requirements
- [x] 4.4 Confirm that no existing spec requirement contradicts Decision 8 (no live schema changes in this wave)

## 5. Archive and Spec Promotion

- [x] 5.1 Run `openspec validate --change design-cel-expression-model` and fix any validation errors
- [x] 5.2 Run `openspec archive design-cel-expression-model` to promote `specs/cel-expression-model/spec.md` delta into `openspec/specs/cel-expression-model/spec.md`
- [x] 5.3 Run `openspec validate --all` and confirm every spec under `openspec/specs/` passes validation
- [x] 5.4 Confirm `openspec/specs/cel-expression-model/spec.md` contains `## Purpose` and `## Requirements` sections (not the raw delta header)
