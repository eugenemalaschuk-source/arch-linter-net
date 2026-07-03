## Why

Issues #133/#137/#138/#140/#141 recovered ArchLinterNet's own Core architecture (application seam, contract execution, scanning/discovery/resolution internals, adapter isolation). That recovered shape is not yet fully encoded back into `architecture/dependencies.arch.yml`, so nothing stops future work from silently reintroducing an application-seam bypass, an upward dependency from scanning/discovery/resolution, or an adapter reaching into contract-execution internals. #142 is the final guardrail task for #132 and closes that gap using only capabilities the product already supports.

## What Changes

- Add a `core-validation-must-not-bypass-application-internals` dependency rule: `core_validation` (the CLI/Testing/Unity-facing application seam) must not depend directly on `core_scanning`, `core_discovery`, or `core_resolution` — it must route through `core_execution`/`core_asmdef` instead. No production code changes are needed; current code already satisfies this, the rule only closes a coverage gap that let it regress silently.
- Add a `core-discovery-must-not-depend-on-execution-or-validation` dependency rule, mirroring the existing `core_resolution`/`core_scanning` rules, so `core_discovery` is symmetrically forbidden from depending upward on `core_execution`/`core_validation`.
- Add `strict_protected` rules `core-discovery-internals-are-protected` and `core-resolution-internals-are-protected` (mirroring the existing `core-scanning-internals-are-protected`), so `core_discovery` and `core_resolution` internals stay reachable only from within `ArchLinterNet.Core`, not from any future host adapter.
- Add a `strict_coverage` `scope: rule_input` contract (`self-policy-rule-input-coverage`) covering the seam/isolation/leaf dependency and protected-surface rule IDs, so a rule that stops matching any code (a typo'd layer, a renamed namespace) is caught instead of silently passing.
- Document, in the `self-architecture-policy` spec, that static-production-service and god-object guardrails are enforced through `docs/internal/static-class-inventory.md` (code-review-governed) rather than a YAML contract, because ArchLinterNet has no contract family that detects `static class` declarations or type/member-count size — inventing one is out of scope for this issue (`supported-capabilities.md` explicitly lists this as unsupported).
- Document that adding a new contract-family implementation to the engine must come with either a corresponding self-policy rule in `architecture/dependencies.arch.yml` or a written exception, as a contribution-process requirement (docs, not a new engine capability).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `self-architecture-policy`: add requirements for the application-seam-bypass rule, the `core_discovery` upward-dependency rule, the `core_discovery`/`core_resolution` protected-surface rules, the rule-input self-policy coverage contract, and the documented (non-YAML) static-service/god-object/new-contract-family guardrail process.

## Impact

- `architecture/dependencies.arch.yml`: five new contract entries (two `strict`, two `strict_protected`, one `strict_coverage` rule-input).
- `openspec/specs/self-architecture-policy/spec.md`: new requirements via archive.
- No `src/` production code changes expected — the new rules codify the already-recovered architecture.
- `make lint-architecture` / `task acceptance:fresh` must continue to pass against the repository's own build output.
