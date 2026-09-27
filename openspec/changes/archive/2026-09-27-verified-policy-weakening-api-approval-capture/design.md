## Context

See proposal.md for motivation and `specs/policy-weakening-guardrails/spec.md` for the behavior contract. `PublicApiCaptureRequest` already accepts a condition set, preparation mode, and no-restore setting. The debt-gate and health paths forward their request values, while `policy weakening` currently leaves those capture fields at their defaults. The public API resolver supports `EnsureBuilt` and verifies the post-build runner; it has no prepared-receipt consumer option.

## Goals / Non-Goals

**Goals:**

- Make approved live API capture use the same explicit build-state request fields as the gate and health callers.
- Prevent an approval from using unverified ordinary artifacts.
- Keep no-approval comparisons artifact-only.

**Non-Goals:**

- Change approval JSON, context digests, API snapshots, comparison rules, or the public API surface resolver.
- Add prepared-receipt reuse where the capture request currently has no such capability.
- Update a consumer repository or release a new package from this change.

## Decisions

- Add the existing build-state inputs to the `policy weakening` command surface: `--condition-set`, `--ensure-built`, and `--no-restore`. Map them into `PublicApiCaptureRequest` for each approved contract. This uses the existing resolver rather than introducing a second build or API scanner.
- Require `--ensure-built` when approvals are present and reject before capture otherwise. Defaulting silently to `EnsureBuilt` would hide build work and make the requested execution mode implicit; allowing ordinary mode would let stale assemblies be presented as current CLR evidence.
- Leave context comparison and approval matching unchanged. Complete verified capture supplies the existing comparer with the evidence it already requires; incomplete, extra, stale, or mismatched evidence remains blocking.
- Keep prepared-receipt reuse out of this change because `PublicApiCaptureRequest` and its resolver do not expose that infrastructure. The command accepts `--no-restore` for the supported verified-build path.

## Risks / Trade-offs

- Existing scripts that pass `--public-api-approval` without `--ensure-built` will now fail closed. The command help and CLI reference explain the required option; consumers can add `--ensure-built` and, where appropriate, `--no-restore`.
- A verified build may cost more time than ordinary capture. Correctness of approval evidence takes precedence; restore can be skipped when dependencies are already available.
- Consumer wrappers must opt into the new flag after using a package that contains this change. Consumer pin updates remain separate from this upstream implementation.

## Migration Plan

Update callers that use `policy weakening --public-api-approval` to pass `--ensure-built`, the intended `--condition-set`, and `--no-restore` only when restore is not wanted. After this change is merged and made available through a consumable package, update the server wrapper/pin in its own consumer change and rerun its canonical PR checks. Rolling back the tool version restores the former command behavior; the approval artifact format remains unchanged.
