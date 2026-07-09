## Context

`architecture/dependencies.arch.yml` already governs `core_execution` (`core-execution-must-not-depend-on-hosts`), `core_reporting`/`core_model` (leaves), and `core_resolution`/`core_scanning`/`core_discovery` (forbidden from depending upward). `core_contracts` has no equivalent rule today — only two `strict_external` entries forbidding it from the DI container and MSBuild. Verified against the current source tree:

- `grep -rl "ArchLinterNet.Core.Execution" src/ArchLinterNet.Core/Contracts/` matches exactly one file, `ArchitectureContractFamilyBinding.cs`, and only inside a comment: *"Deliberately separate from `ArchLinterNet.Core.Execution.ArchitectureContractFamilyDescriptor`: Contracts must not depend on Execution ... so the two registries duplicate the family list rather than sharing one across the module boundary."* No `using ArchLinterNet.Core.Execution` exists anywhere under `Contracts/`. The boundary the design relies on is real but currently unenforced by tooling — a future edit could add the "obvious" shared-registry `using` and nothing would catch it.
- No `Contracts` file references `ArchLinterNet.Cli`/`ArchLinterNet.Testing`/`ArchLinterNet.Unity`. Hosts are already blocked from importing `core_contracts` directly by the three existing `*-must-use-*-application-seam` rules (which list `core_contracts` in their `forbidden` set), but nothing stops `core_contracts` from importing a host in the other direction.

The `#208–#216` refactor chain (contract-family descriptor registry, checker registry, extracted session checkers, configuration-inspection contributors, typed diagnostic payloads, decoupled YAML contract groups, extracted family validation) is complete and archived. Its capability specs (`contract-family-registry`, `contract-handler-execution`, `family-checker-extraction`, `configuration-contributor-registry`, `diagnostics-model`, `policy-document-validation-pipeline`) already describe the target shape; this change does not modify them, it adds the self-policy guardrails that keep that shape from regressing, matching how `openspec/changes/archive/2026-07-03-add-self-policy-guardrails/` (#142) closed the previous refactor chain (#132–#141).

Two categories of regression the issue names have no expressible YAML rule: "a central file grew a new inline branch instead of a new descriptor/file" and "a dispatcher regrew an if-chain." `docs/policy-format/supported-capabilities.md` enumerates every supported contract family (dependency, layer, allow-only, cycle, independence, protected-surface, external-dependency, method-body forbidden-API, asmdef, coverage) and explicitly lists "unrestricted custom contract families outside the documented YAML schema" as unsupported — there is no family that inspects file structure, branch count, or a dispatch pattern's shape.

## Goals / Non-Goals

**Goals:**
- Close the two expressible coverage gaps (`core_contracts` host-isolation, `core_contracts`/`core_execution` independence) using existing, already-used contract families (`strict`), and extend the existing rule-input coverage contract to include them.
- Record, in the `self-architecture-policy` spec, that the four extension-hotspot regressions named in #215 (central catalog/binding branch growth, god-session regression, diagnostic-mapper dispatch regrowth, YAML DTO regrowth) are intentionally documentation-governed, pointing at a new guardrail-candidate paragraph in `docs/internal/core-architecture-blueprint.md`.
- Extend the existing "new contract-family implementations require self-policy coverage" requirement to name the concrete extension namespaces the #208–#216 chain established, so future contributors have an unambiguous checklist.

**Non-Goals:**
- Inventing a new ArchLinterNet contract family (file-structure/branch-growth/dispatch-shape detection). Confirmed unsupported by the product and out of scope for a self-policy issue, same conclusion as #142 reached for static-class detection.
- Any `src/` production code change — the recovered #208–#216 architecture already satisfies every new rule; this change only adds guardrails that catch a future regression.
- New user-facing contract semantics or YAML schema changes (explicit non-goal in the issue).
- Reopening #132 or performing a broad self-policy redesign (explicit non-goals in the issue). CLI command growth (#202) and abstraction namespace conventions (#201) are already tracked/complete elsewhere and are not touched here.
- Adding sub-namespace layers for `Execution.Checkers`, `Execution.Abstractions`, or `Contracts.Families`/`Contracts.Validators`. They already inherit their parent layer's (`core_execution`/`core_contracts`) dependency-direction rules; splitting the layer definitions further is a speculative refactor with no issue-cited need, mirroring the #142 design's decision not to split `core_execution` into handler/checker sub-namespaces.

## Decisions

- **Add `core-contracts-must-not-depend-on-hosts` as a `strict` rule** (source: `core_contracts`, forbidden: `[cli, testing, unity]`), shaped identically to `core-execution-must-not-depend-on-hosts`, so `Contracts.Families`/`Contracts.Validators` get the same host-isolation guarantee `Execution.Checkers`/`Execution.Abstractions` already have.
- **Add `core-contracts-must-not-depend-on-execution` as a `strict` rule** (source: `core_contracts`, forbidden: `[core_execution]`) rather than a `strict_protected` rule on `core_execution`, because `core_execution` is legitimately imported elsewhere (by hosts through the seam, internally by its own checkers); the constraint being enforced is specifically "Contracts must not reach into Execution," which is a source-side rule on `core_contracts`, not a protected-surface rule on `core_execution`. This directly turns the `ArchitectureContractFamilyBinding.cs` comment into an enforced boundary.
- **Extend `self-policy-rule-input-coverage`'s existing `contract_ids` list** with both new rule IDs rather than adding a second `strict_coverage` rule-input contract, matching the single-contract pattern already established and keeping the policy file additions minimal.
- **Do not add YAML rules for the four extension-hotspot regressions** (central catalog/binding branch growth, god-session regression, diagnostic-mapper dispatch regrowth, YAML DTO regrowth). Instead extend the `self-architecture-policy` spec with a documentation-governed requirement pointing at a new "Guardrail candidate for #215" paragraph in `docs/internal/core-architecture-blueprint.md`, following exactly the existing pattern used for the #142 `*.Abstractions` guardrail paragraph (line 214) and the static-class-inventory guardrail (already in the spec).
- **Extend, rather than replace, the existing "new contract-family implementations require self-policy coverage or a documented exception" requirement** by naming `Execution.Checkers`, `Execution.Abstractions`, `Contracts.Families`, `Contracts.Validators`, and `Model` payload records as the required homes for new family code, so the requirement's language keeps pace with the now-completed #208–#216 refactor instead of only referring to the pre-refactor shape.

## Risks / Trade-offs

- [Adding `core-contracts-must-not-depend-on-execution` could false-positive if any `Contracts` file has a real (non-comment) dependency on `Execution` today] → Verified via `grep -rl "ArchLinterNet.Core.Execution" src/ArchLinterNet.Core/Contracts/`: the only match is a comment in `ArchitectureContractFamilyBinding.cs`; no `using` directive or type reference exists. Re-verified with `make lint-architecture` after the rule is added, before finalizing tasks.
- [Adding `core-contracts-must-not-depend-on-hosts` could false-positive if a `Contracts` file references a host namespace] → Verified via `grep -rln "ArchLinterNet\.(Cli|Testing|Unity)" src/ArchLinterNet.Core/Contracts/`: zero matches.
- [Rule-input coverage contract could flag the two new rule IDs as `stale`/`unresolved` if `core_contracts`'s layer definition doesn't resolve as expected] → `core_contracts` is an existing, already-used layer (`ArchLinterNet.Core.Contracts`, declared at policy line 19–20 and already the source of two `strict_external` rules), so it resolves the same way `core_execution` already does for its own host-isolation rule.
- [Documentation-only enforcement for the four extension-hotspot guardrails is weaker than a YAML gate] → Accepted trade-off, same as #142's static-service/god-object precedent: the alternative is building unsupported engine capability, which is explicitly out of scope per the issue's non-goals.

## Migration Plan

Pure policy-file and spec/doc addition; no code migration. Run `make fmt` and `make acceptance` after editing `architecture/dependencies.arch.yml` to confirm the repository still validates against its own tightened self-policy. Rollback is a straight revert of the YAML/spec/doc diff if `make acceptance` cannot be made to pass without an unexpected production code change.

## Open Questions

None — both new contract entries were verified against the current `src/Contracts/` tree before writing this design; no existing dependency requires a code change to satisfy the new rules.
