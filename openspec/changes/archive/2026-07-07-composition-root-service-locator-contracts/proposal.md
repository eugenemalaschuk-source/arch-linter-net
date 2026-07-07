## Why

Dependency injection registration and service-locator usage (`IServiceProvider.GetService`, ASP.NET DI registration APIs, VContainer/Unity-style container APIs) should stay inside declared composition/bootstrap layers, but ArchLinterNet has no way to statically enforce that boundary. Issue #88 asks for declarative composition-root and service-locator usage contracts so these leaks are caught the same way other boundary violations already are, without attempting runtime DI resolution (explicitly out of scope).

## What Changes

- Add a **composition contract family** (`contracts.strict_composition` / `contracts.audit_composition`): forbid selected APIs (`forbidden_apis`, matched with the same call-pattern vocabulary as method-body contracts — namespace prefixes, member names, `Type.Member`, fully-qualified members) from being called by any type outside a declared set of allowed composition layers/namespaces/projects/assemblies (`allowed_only_in_layers`, `allowed_only_in_namespaces`, `allowed_only_in_projects`, `allowed_only_in_assemblies` — the same location vocabulary and `IsAllowedLocation` semantics used by interface-implementation contracts).
- The contract accepts `id`, `name`, `reason`, and `ignored_violations`, mirroring every other family.
- Emit deterministic diagnostics identifying the violating type/member, the matched forbidden API, and the expected composition boundary.
- Support strict (fails validation) and audit (reports only) usage via parallel `strict_composition`/`audit_composition` groups.
- Update JSON schema, capabilities metadata, docs, sample policies, and AI policy-authoring guidance.

No breaking changes: existing contract families are untouched; new YAML keys are additive.

## Capabilities

### New Capabilities
- `composition-contracts`: forbid selected composition-root/service-locator API calls for any type outside declared allowed composition layers/namespaces/projects/assemblies, with strict/audit modes, ignores, and deterministic diagnostics. Explicitly does not perform runtime DI resolution or prove registration correctness.

### Modified Capabilities

(none — existing contract family requirements are unchanged; the new family is additive)

## Impact

- `src/ArchLinterNet.Core/Contracts/` — new `ArchitectureCompositionContract` model class + group properties on `ArchitectureContractGroups`.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.*` — fail-closed load-time validation (at least one `forbidden_apis` entry and at least one allow-list entry).
- `src/ArchLinterNet.Core/Execution/` — catalog entry, handler, `ArchitectureAnalysisSession.Composition.cs` partial, policy-consistency layer-name wiring.
- `src/ArchLinterNet.Core/Scanning/ArchitectureIlMethodBodyScanner.cs` — extract the shared per-type IL method-scanning loop into a reusable method that can run against an arbitrary type set (not just a single source-layer-selected set).
- `src/ArchLinterNet.Core/Model` + `Reporting` — new diagnostic kind/record, violation fields, mapper/formatter support.
- `schema/dependencies.arch.schema.json`, `archlinternet.capabilities.json` — schema and capability metadata.
- `docs/` — new `docs/contracts/composition.md`, plus index/policy-format/AI guidance updates.
- Sample policies — a server composition-root example and a Unity/VContainer-style bootstrap example.
- `tests/ArchLinterNet.Core.Tests` — fixtures and tests (allowed composition usage, forbidden service-locator usage, ASP.NET DI example, Unity/VContainer-style example, strict failure, audit-only, ignores, loader validation, deterministic ordering).
