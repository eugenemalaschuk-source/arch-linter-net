## Context

`architecture/dependencies.arch.yml` already encodes most of the recovered Core architecture from #133/#137/#138/#140/#141: the CLI/Testing/Unity application-seam rules, `core_execution`'s isolation from adapters, `core_reporting`/`core_model` as leaves, and `core_resolution`/`core_scanning` forbidden from depending upward on `core_execution`/`core_validation`. Verified against the current source tree (grep of `using ArchLinterNet.Core.*` across `src/`):

- `core_discovery` is imported only by `core_execution` and `Composition/ServiceCollectionExtensions.cs` — no adapter, no `core_validation`, no `core_scanning`/`core_resolution` peer.
- `core_resolution` is imported by `core_scanning`, `core_execution`, `core_asmdef`, and `Contracts/ArchitectureContractModels.cs` — never by `core_validation`.
- `core_validation`'s `using` list is `Composition`, `Contracts`, `Contracts.Abstractions`, `Execution`, `Execution.Abstractions`, `Model`, `Reporting`, `Validation.Abstractions` — no `Scanning` or `Discovery` reference.

So the gaps between the issue's bullet list and the current policy are pure coverage gaps (missing rules), not code that needs to change.

Two of the issue's bullets — "static production services forbidden except an explicit allowlist" and "god-object growth is prevented" — cannot be encoded as a new YAML contract. `docs/policy-format/supported-capabilities.md` enumerates every contract family ArchLinterNet supports (dependency, layer, allow-only, cycle, independence, protected-surface, external-dependency, method-body forbidden-API, asmdef, coverage) and explicitly lists "unrestricted custom contract families outside the documented YAML schema" as unsupported. There is no family that inspects a `static` modifier or counts type/member size. `openspec/specs/static-production-service-inventory/spec.md` already exists and states it is "the source of truth for #142's self-policy guardrails on static production services" — it governs this through `docs/internal/static-class-inventory.md`, a reviewed classification document, not a policy rule.

## Goals / Non-Goals

**Goals:**
- Close the four expressible coverage gaps (`core_validation` seam-bypass, `core_discovery` upward dependency, `core_discovery`/`core_resolution` protected-surface) using existing, already-used contract families.
- Add a rule-input coverage contract so these seam rules can't silently stop matching any code without being flagged.
- Record, in the `self-architecture-policy` spec, that the static-service and god-object guardrails are intentionally documentation-governed (pointing at the existing inventory), and that new contract-family engine work must ship with a self-policy rule or a written exception.

**Non-Goals:**
- Inventing a new ArchLinterNet contract family (static-class detection, member-count/god-object detection). Confirmed unsupported by the product and out of scope for a self-policy issue.
- Any `src/` production code change — the recovered architecture already satisfies every new rule; this change only adds the guardrails that catch a regression.
- Restructuring `core_execution`'s flat namespace into handler/checker sub-namespaces. `IArchitectureContractHandler`/`ArchitectureContractHandlerRegistry` already isolate the handler boundary at the type level within `core_execution`, which is itself already walled off from every adapter by three existing dependency rules (`cli-must-use-validation-application-seam`, `testing-must-use-validation-application-seam`, `core-execution-must-not-depend-on-cli-or-testing`). Splitting the namespace further is a speculative refactor with no issue-cited need.
- Runtime DI validation (explicit non-goal in the issue).

## Decisions

- **Add `core-validation-must-not-bypass-application-internals` as a `strict` dependency rule** (source: `core_validation`, forbidden: `[core_scanning, core_discovery, core_resolution]`) rather than a `strict_protected` rule on those three targets, because `core_execution` and `core_asmdef` (both allowed to use scanning/discovery/resolution as implementation details) must remain unaffected — a protected-surface rule with `allowed_importers: [core]` would not distinguish `core_validation` from `core_execution`. A source-side dependency rule on `core_validation` is the narrowest fit.
- **Add `core-discovery-must-not-depend-on-execution-or-validation`** as a `strict` rule mirroring the existing `core-resolution-must-not-depend-on-execution-or-validation` / `core-scanning-must-not-depend-on-execution-or-validation` rules verbatim in shape (forbidden: `[core_execution, core_validation, cli, testing, unity]`), for consistency with the two existing internals-layer rules and because the issue explicitly names discovery alongside scanning/resolution.
- **Add `strict_protected` rules for `core_discovery` and `core_resolution`** mirroring `core-scanning-internals-are-protected` (`allowed_importers: [core]`). This is defense-in-depth against a future adapter reaching into these internals directly (today's dependency rules already block CLI/Testing/Unity from doing so via the seam rules, but the protected-surface rule makes the internals-are-Core-only invariant explicit and self-documenting the way `core_scanning`'s already is) — this is the "namespace/type placement" mechanism the issue names for god-object prevention: it structurally prevents any one host from accumulating direct access to internal implementation types.
- **Add one `strict_coverage` `scope: rule_input` contract** referencing the seam/leaf/protected contract IDs added or already present for the recovered architecture, rather than one coverage contract per rule, matching the single rule-input coverage example pattern in `docs/contracts/coverage.md` and keeping the policy file additions minimal.
- **Do not add a YAML rule for static-service or god-object detection.** Instead extend the `self-architecture-policy` spec with a requirement that documents `docs/internal/static-class-inventory.md` as the enforcement mechanism, satisfying the issue's "explicit allowlist" and "documented" language without violating `supported-capabilities.md`.

## Risks / Trade-offs

- [Adding `core_discovery`/`core_resolution` to `strict_protected` could false-positive if a currently-untracked adapter or sample project references them] → Verified via `grep -rl "ArchLinterNet.Core.Discovery"` / `"ArchLinterNet.Core.Resolution"` across `src/` before writing the rule; only `core`-internal files (Execution, Scanning, Asmdef, Composition, Contracts) import them today, all covered by `allowed_importers: [core]`.
- [Rule-input coverage contract could flag existing rules as `stale`/`unresolved` if a referenced layer doesn't currently match code] → All referenced contract IDs' `source`/`forbidden`/`protected` layers correspond to namespaces with real code today (verified above); if `task acceptance:fresh` disagrees, the contract list will be adjusted before merging, not silently loosened.
- [Documentation-only enforcement for static-service/god-object guardrails is weaker than a YAML gate] → Accepted trade-off: the alternative is building unsupported engine capability, which is explicitly out of scope; the inventory document is already reviewer-facing and lists 0 outstanding "follow-up candidate" static services as of #159.

## Migration Plan

Pure policy-file and spec addition; no code migration. Run `make fmt` and `task acceptance:fresh` after editing `architecture/dependencies.arch.yml` to confirm the repository still validates against its own tightened self-policy. Rollback is a straight revert of the YAML/spec diff if `task acceptance:fresh` cannot be made to pass.

## Open Questions

None — all five new contract entries were verified against current `src/` structure before writing this design.
