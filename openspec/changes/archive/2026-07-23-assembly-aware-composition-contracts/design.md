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
- Introducing a new "shared selector" abstraction (assembly/namespace/type/member/project/semantic-role) for the composition boundary itself. The boundary (`allowed_only_in_layers/namespaces/projects/assemblies`) is already assembly-aware for membership testing (`actualAssemblyName` is already checked against `allowedAssemblyNames`); this change is scoped to violation *identity*, not to redesigning boundary selection. Extending `ArchitectureContextSelector`/semantic-role identity to composition boundaries is a separate, larger change and out of scope here. Concretely: today the composition boundary can already scope down to a single host's `Program` type in the common case where each host's `Program` lives in its own namespace (as the issue's own `Product.Api.Program`/`Product.Admin.Program` fixture does) — `allowed_only_in_namespaces` already isolates it. A boundary selector granular enough to allow *one specific type* while excluding the rest of its assembly/namespace (e.g. `allowed_only_in_types: [Host.Api.Program]`) is a real, distinct feature gap, but no `allowed_only_in_*` selector across *any* contract family in this codebase currently supports type-level granularity — adding it here would be a scope expansion beyond what #360's acceptance criteria and fixture describe (same-named-type non-collision, not boundary-selector granularity). Tracked as follow-up backlog scope, not folded into this change.
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

No document/schema migration. This only changes the identity computed for *new* validation runs; existing `version: 2` baseline entries for composition contracts (if any) were generated under the old fallback identity (`SourceAssembly: null`) and will no longer match post-change live identities (which now carry `SourceAssembly`). Unlike the #357/#381 v1→v2 transition, `baseline migrate` does **not** apply here — it refuses to run against a file that already declares `version: 2` (its job is specifically the v1→v2 identity upgrade, not re-qualifying an already-v2 file whose fields were previously left null). The documented path for this case (`docs/guides/migration-baselines.md`) is `baseline update` (adds fresh, fully-qualified entries) followed by `baseline prune` (removes the now-stale, unqualified originals, since they no longer structurally match any current violation) — not a bare "re-run migrate/update" as originally stated.

## Review follow-up (post-initial-implementation)

A self-review after the initial implementation landed found three additional gaps, fixed in the same change before merge:

1. **Occurrence-collapsing bug**: `CheckCompositionContract` called `.Distinct()` on the raw IL match list *before* calling `IsIgnored`, so two distinct call sites to the same forbidden API from the same source member collapsed into a single match before ever reaching the occurrence counter — baselining one occurrence therefore silently suppressed both real call sites. Fixed by moving the dedup to *after* the `IsIgnored`/occurrence check (matching the pattern `ArchitectureSourceScanner` already uses for method-body contracts): every raw call site independently reaches `IsIgnored`, and only the resulting violation *list* dedupes to one entry per `(type, source member, matched API)` tuple, preserving the existing diagnostic contract while fixing occurrence discrimination underneath it.
2. **Assembly qualified in baseline identity but invisible in diagnostics**: `SourceAssembly` was threaded into `ArchitectureViolationIdentity` but never onto `ArchitectureViolation`/`CompositionPayload`, so two same-named types in different assemblies still looked identical in human/JSON/`--explain` output even though baselining now distinguished them correctly. Added `SourceAssembly` to `CompositionPayload`/`CompositionDiagnostic` and surfaced it in `FormatCompositionContextForHumans`/`ApplyCompositionCiFields` (human + `--json` + `--explain`, which share the same formatter). SARIF is intentionally not touched — no contract family exposes this kind of per-violation member/assembly detail in SARIF properties today except `FrameworkReference`'s dedicated evidence array; that is a separate, broader change.

Point (1) is covered by a new regression test exercising two IL call sites to the same forbidden API within one source member. Point (2)/the migration guidance are documentation-only changes to `docs/contracts/composition.md`/`docs/guides/migration-baselines.md`.
