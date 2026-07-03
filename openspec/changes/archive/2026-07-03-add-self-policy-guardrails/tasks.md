## 1. Policy rules

- [x] 1.1 Add `core-validation-must-not-bypass-application-internals` to `strict` (source: `core_validation`, forbidden: `[core_scanning, core_discovery, core_resolution]`)
- [x] 1.2 Add `core-discovery-must-not-depend-on-execution-or-validation` to `strict` (source: `core_discovery`, forbidden: `[core_execution, core_validation, cli, testing, unity]`)
- [x] 1.3 Add `core-discovery-internals-are-protected` and `core-resolution-internals-are-protected` to `strict_protected` (`allowed_importers: [core]`), mirroring `core-scanning-internals-are-protected`
- [x] 1.4 Add `self-policy-rule-input-coverage` to `strict_coverage` (`scope: rule_input`) referencing the seam/leaf/protected contract IDs for the recovered architecture

## 2. Documentation

- [x] 2.1 Cross-check `docs/internal/static-class-inventory.md`'s "Guardrail candidates for #142" section is still accurate against current `src/` (no new unclassified static production services) — verified: every `static class` in `src/` matches the inventory's classification exactly; no new debt
- [x] 2.2 Confirm no README/docs cross-reference needs updating for the new contract IDs (`docs/policy-format`/`docs/contracts` describe generic contract shape, not this repo's specific rule set — verify no update needed) — confirmed, no update needed

## 3. Validation

- [x] 3.1 Run `make fmt` — no changes needed
- [x] 3.2 Run `make acceptance` (repo has no Taskfile; this is the repo's equivalent gate — lint + all tests) — exit 0, 632 tests passed, self-policy validated against tightened rules
- [x] 3.3 Confirm `openspec validate --all` passes after spec sync — 56/56 passed

## 4. Spec sync and archive

- [ ] 4.1 Run `openspec archive add-self-policy-guardrails` after implementation and tests pass
- [ ] 4.2 Verify `openspec/specs/self-architecture-policy/spec.md` reflects the new requirements
