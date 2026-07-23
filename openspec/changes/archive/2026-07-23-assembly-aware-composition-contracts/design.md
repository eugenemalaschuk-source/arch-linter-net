## Context

`ArchitectureAnalysisSession.CheckCompositionContract` (`src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.Composition.cs`) already resolves `actualAssemblyName = type.Assembly.GetName().Name` per candidate type (line 45) to decide composition-boundary membership (`IsAllowedLocation`), but the violation it builds only carries `sourceType = ArchitectureTypeNames.SafeFullName(type)` (namespace + type name). The subsequent call:

```csharp
if (executionContext.IsIgnored(sourceType, matchedForbiddenApi))
```

passes none of `IsIgnored`'s optional `sourceAssembly`/`sourceMember`/`targetAssembly`/`targetMember` parameters. Per the already-generic mechanism built in #357/#381 (`ArchitectureContractExecutionContext.BuildLiveIdentity`), an omitted `sourceAssembly` means the resulting `ArchitectureViolationIdentity.SourceAssembly` is `null`, and the family falls back to the pre-#357 `(contract family, contract id, source_type, target_type)` discrimination — indistinguishable across assemblies. `ArchitectureViolationIdentity.ResolveKind` also has no case for `"composition"`, so it falls through to the generic `"reference"` kind rather than `"call"` (the shape `method_body` already uses for the same style of static call-site detection).

## Goals / Non-Goals

**Goals:**
- Populate `SourceAssembly`, `SourceMember`, and `TargetMember` on composition violations' `ArchitectureViolationIdentity`, using the exact same additive `IsIgnored` parameters other qualified families already use — no new mechanism.
- Classify composition as `Kind = "call"`.
- Preserve every existing composition contract behavior: boundary matching, forbidden-API pattern matching (member/`Type.Member`/FQN/namespace-prefix), diagnostic payload shape, deterministic ordering, ignore/baseline suppression for already-migrated v2 entries.

**Non-Goals:**
- Introducing a new "shared selector" abstraction (assembly/namespace/type/member/project/semantic-role) for the composition boundary itself. The boundary (`allowed_only_in_layers/namespaces/projects/assemblies`) is already assembly-aware for membership testing (`actualAssemblyName` is already checked against `allowedAssemblyNames`); this change is scoped to violation *identity*, not to redesigning boundary selection. Extending `ArchitectureContextSelector`/semantic-role identity to composition boundaries is a separate, larger change and out of scope here.
- Changing `TargetAssembly` for composition — forbidden APIs are framework/library members (e.g. `System.IServiceProvider.GetService`), not local types with a resolvable target assembly in this scanner; `FrameworkReference` contracts follow the same precedent (no `targetAssembly`, only `targetMember`).
- Any change to `version: 1` baseline files or their matching semantics.

## Decisions

### Reuse the existing `IsIgnored` qualification mechanism, no new model

`ArchitectureContractExecutionContext.IsIgnored` already accepts `sourceAssembly`, `targetAssembly`, `sourceMember`, `targetMember`, `configuration` as optional named parameters (added in #381) specifically so each contract family can opt into qualification incrementally. `ArchitectureNamespaceViolationFinder` (dependency-style) and `ArchitectureSourceScanner` (method-body) already exercise this. Composition needs the same one-line change at its single call site — no new record, no new baseline document field, no schema change.

### `Kind = "call"` for composition

`ResolveKind` maps `"method_body" => "call"` because method-body contracts detect static call-site usage of forbidden members. Composition contracts detect the identical shape (IL/reflection call-site scanning against `forbidden_apis` patterns) — the only difference is the boundary is an allow-list scanned from the outside in, rather than a single named source layer. `"call"` is the accurate `Kind` for both.

### `sourceMember`/`targetMember` come from the already-scanned match, not the forbidden-API string alone

`ArchitectureIlForbiddenCallMatch` (produced by `ArchitectureIlMethodBodyScanner.FindMatchDetailsForType`) already exposes `SourceMember` (the calling member) and `MatchedMember` (the matched forbidden API). Passing both into `IsIgnored` as `sourceMember`/`targetMember` means two distinct forbidden calls from different members of the same type — or the same forbidden API called from two different members — get distinct identities without relying solely on the occurrence counter, matching the precision `ArchitectureSourceScanner` already has for method-body contracts.

## Migration

No document/schema migration. This only changes the identity computed for *new* validation runs; existing `version: 2` baseline entries for composition contracts (if any) were generated under the old fallback identity (`SourceAssembly: null`) and will no longer match post-change live identities (which now carry `SourceAssembly`) — exactly the same class of transition #357/#381 already documented and provided `baseline migrate` for. Adopters with existing composition baselines re-run `baseline migrate` (or `baseline update`) as already documented in `docs/guides/migration-baselines.md`; no new migration tooling is needed for this change.
