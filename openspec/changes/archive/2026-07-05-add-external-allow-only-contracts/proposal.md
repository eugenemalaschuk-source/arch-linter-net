## Why

`strict_external`/`audit_external` (external dependency contracts) can only express "these external dependency groups are forbidden." Issue #60 asks for the external-dependency counterpart to the existing first-party `strict_allow_only`/`audit_allow_only`: whitelist semantics so a layer (domain code, Unity runtime code, an infrastructure adapter, tests) can be restricted to a small, finite set of allowed vendor/framework dependency groups instead of only excluding known-bad ones. Blocklists don't scale as new vendor SDKs are introduced; a layer that should only ever use BCL primitives and one approved SDK needs an allow-only shape, not an ever-growing forbidden list.

## What Changes

- Add a new contract family `external_allow_only` (YAML groups `strict_external_allow_only`/`audit_external_allow_only`): a single named source layer may only reference external dependency groups in an `allowed` list; any reference matching a *declared* `external_dependencies` group that is not in `allowed` is a violation, mirroring `ArchitectureAllowOnlyContract`'s `source`/`allowed`/`allowed_types` shape but evaluated against external groups instead of first-party layers.
- New model `ArchitectureExternalAllowOnlyContract` (`Name`/`Id`/`Source`/`Allowed`/`AllowedTypes`/`IgnoredViolations`/`Reason`), registered through the existing contract-catalog + handler-registry seam (`ArchitectureContractGroups`, `ArchitectureContractCatalog.Build`, a new `ExternalAllowOnlyContractHandler`, DI registration) — no `ArchitectureContractExecutor` changes.
- Violation detection reuses the existing `ArchitectureExternalDependencyViolationFinder` (type-level reference matching against `external_dependencies` group namespace/type prefixes) for every *declared* external group not in `allowed`. A declared-but-nonexistent group name in `allowed` simply matches nothing and has no effect — the contract fails closed (nothing is silently permitted by a typo).
- Method-body IL scanning (the `ArchitectureExternalDependencyIlScanner` used by `strict_external`/`audit_external`) is explicitly out of scope for this change, matching how it was added to the forbidden-group family as a separate follow-up after the initial `external_dependencies` contract shipped. Documented as a scope decision, not an oversight.
- BCL/system references are never implicitly flagged: they are only in scope if a policy author explicitly declares an `external_dependencies` group whose prefixes match BCL namespaces (e.g. `System.Net.Http`) and that group is excluded from a contract's `allowed` list. This is identical to how `strict_external`/`audit_external` already treats BCL references, and is documented and tested explicitly here per the issue's acceptance criteria.
- `allowed_types` supports exact full type-name exceptions, mirroring the namespace-level allow-only contract's `allowed_types` field.
- JSON schema (`externalAllowOnlyContract` def + two new contract-group array properties), docs (new `docs/contracts/external-allow-only.md`, contract index, policy-format reference, AI-facing guidance), and tests updated to cover the new family.
- Existing `strict_external`/`audit_external` and `strict_allow_only`/`audit_allow_only` behavior is unchanged; this is purely additive.

## Capabilities

### New Capabilities
- `external-allow-only-contracts`: strict/audit contracts that restrict a named source layer to only referencing an explicitly allowed set of declared external dependency groups (whitelist semantics for vendor/framework references, the external-dependency counterpart to `allow-only-contracts`).

### Modified Capabilities
(none — existing `external-dependency-contracts` and `allow-only-contracts` behavior is unchanged; this change only adds one new, additive contract family alongside them.)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`: new `ArchitectureExternalAllowOnlyContract` model, new `StrictExternalAllowOnly`/`AuditExternalAllowOnly` groups on `ArchitectureContractGroups`.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.Checking.cs`: new `CheckExternalAllowOnlyContract`.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractCatalog.cs`: two new `AddGroup` calls.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs`: new `ExternalAllowOnlyContractHandler`.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: one new DI registration.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`: wire new groups into duplicate-ID validation.
- `schema/dependencies.arch.schema.json`: new array properties and `$defs.externalAllowOnlyContract`.
- `docs/contracts/external-allow-only.md` (new), `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`, `docs/ai/capabilities.md`, `docs/ai/policy-authoring-guide.md`.
- New tests under `tests/ArchLinterNet.Core.Tests/`.
