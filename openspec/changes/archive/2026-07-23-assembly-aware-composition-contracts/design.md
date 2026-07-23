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
- Reusing `ArchitectureContextSelector`/`ArchitectureLayerSelector` (role + metadata + CEL `when`) for the new type-level boundary selector. Both existing "shared selector" types require semantic-role classification (a `role` resolved through attribute extraction) to be useful — the issue's clarification explicitly rules that out for the top-level `Program` case ("without requiring semantic classification through assembly attributes"). `allowed_only_in_types` is therefore a small, purpose-built structured selector (`assembly` + `type`, exact string match only) alongside the existing `allowed_only_in_*` lists, not a reuse of the CEL-based selector.
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

## Second review round: assembly+type selector and SARIF parity

A second review round, backed by an explicit issue clarification ("a global top-level `Program` must be selectable directly by assembly and type identity without requiring semantic classification through assembly attributes... it must share the common selector model and include assembly in diagnostics/baseline identity"), treated two items from the first round's Non-Goals as required scope rather than follow-up:

1. **`allowed_only_in_types` selector** — added `ArchitectureCompositionTypeSelector` (`assembly` + `type`, both required, exact string match) as a fifth composition boundary list alongside `allowed_only_in_layers/namespaces/projects/assemblies`. A type is inside the boundary if its exact `(assembly, type)` pair matches any entry, independent of its assembly/namespace otherwise being in or out of the boundary — this is what lets one host's `Program` be the boundary without exempting the rest of its assembly. Schema (`schema/dependencies.arch.schema.json`), `CompositionValidator` (both-fields-required check, boundary-non-empty check), `CheckCompositionContract` (boundary matching, `DescribeCompositionBoundary` display), and docs were all updated together.
2. **SARIF parity** — `ArchitectureSarifFormatter.BuildProperties` gained a `CompositionDiagnostic` branch exposing `source_assembly`/`source_member`/`matched_forbidden_api`/`expected_composition_boundary` as SARIF result `properties`, matching what human/JSON/explain already carried. This is composition-specific (not a blanket SARIF properties rollout for every family) — it satisfies #360's explicit "human, JSON, SARIF and explain" parity requirement for this family without redesigning SARIF output for unrelated families.

A CI lint failure (IDE1006 naming rule on two local `const string` variables in the occurrence regression test — this repo's style requires PascalCase even for method-local constants) was also fixed in the same round.
