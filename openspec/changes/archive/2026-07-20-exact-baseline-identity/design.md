## Context

Baseline identity today is a flat `(source_type, forbidden_reference)` string pair, built by `ArchitectureBaselineComparer.BuildComparisonKey` (`$"{contractId}|{sourceType}|{forbiddenReference}"`) and `ArchitectureBaselineGenerator`'s dedup key (`$"{SourceType}|{ForbiddenReference}"`). Every contract family funnels through the single choke point `ArchitectureContractExecutionContext.IsIgnored(sourceType, forbiddenReference)`, which both checks glob-based suppression and records `ArchitectureBaselineCandidate(ContractGroup, ContractId, SourceType, ForbiddenReference)`.

Two concrete collisions exist today:
- Dependency-style contracts pass plain type full names with no assembly qualifier, so two same-named types in different assemblies (e.g. two `Program` types in two host assemblies) are indistinguishable.
- Method-body/call contracts (`ArchitectureSourceScanner`) synthesize `forbidden_reference` as `"line {N}: {pattern} -> {symbol}"` — the source line number is the only occurrence discriminator, and it is embedded in what is effectively a display string, not a stable identity field. A refactor that shifts line numbers silently changes which occurrences are "known" vs "new".

The baseline document has a single hardcoded `version: 1` gate (`ArchitectureBaselineLoadingService` throws on anything else); there is no migration path and no `baseline migrate` subcommand.

## Goals / Non-Goals

**Goals:**
- Introduce a versioned, structured `ArchitectureViolationIdentity` that is the actual identity used for baseline matching/generation/dedup, replacing the flat string-pair key.
- Preserve existing `version: 1` baseline files' current (legacy, string-pair) matching behavior exactly — no silent reinterpretation.
- Qualify dependency-style and method-body/call contracts with source/target assembly, target member/overload, and a stable non-line occurrence discriminator, so the two collision scenarios above cannot happen.
- Add a deterministic, fail-closed `baseline migrate` command that upgrades `version: 1` files to `version: 2`.
- Expose `new`/`matched`/`stale`/`ambiguous` status as a structured `status` field (not prose) in `--json` output for `diff`/`verify`/`migrate`.

**Non-Goals:**
- Full assembly/member qualification for every baseline-capable contract family. This change qualifies dependency-style and method-body/call contracts (the two families the P0 comment and adopter scenarios name). Other families (cycles, acyclic-siblings, coverage, independence, protected, external, allow-only, context-dependency, port-boundary, etc.) get a v2-shaped identity (family + contract id + source/target type, versioned) without new assembly/member fields in this change — they are no less precise than today, just carried in the new structured shape. Expanding qualification to these families is explicit follow-up scope.
- Automatic/normal-validation baseline auto-updates.
- Replacing full strict validation with baseline-only checking.
- Redefining #121's delta key — #121 is expected to reuse `ArchitectureViolationIdentity` later; no action needed here beyond keeping the type generally reusable (informational only).
- Using source line number as identity for any family (explicit issue non-goal).

## Decisions

### 1. `ArchitectureViolationIdentity` shape

New record in `src/ArchLinterNet.Core/Model/ArchitectureViolationIdentity.cs`:

```csharp
public sealed record ArchitectureViolationIdentity(
    int IdentityVersion,
    string ContractFamily,
    string Kind,
    string ContractId,
    string? SourceAssembly,
    string SourceType,
    string? SourceMember,
    string? TargetAssembly,
    string? TargetType,
    string? TargetMember,
    int Occurrence,
    string? Configuration = null);
```

- `Kind` is a denormalized, stable classification (`dependency`, `reference`, `call`, `package`, `framework`, `api_change`, `coverage`) independent of the internal `FamilyId` taxonomy, so identity stays stable even if family registration details change.
- `Occurrence` defaults to `0` and is only incremented when multiple candidates share every other field within the same source unit (i.e. it is a collision-breaker, not a primary key component in the common case) — this keeps generated output stable when nothing structurally ambiguous exists, while still discriminating genuinely repeated calls.
- `Configuration`/TFM is left null for all families in this change (no family here is condition-set-sensitive enough to need it); the field exists so later families (package/framework) can populate it without another version bump.

Rejected alternative: hashing the identity into a single opaque string (e.g. SHA-based key). Rejected because the issue requires structured fields to be exposed in JSON individually ("without message parsing"), and a hash would have to be un-hashed for that — keeping it a structured record with a derived `.ToLegacyKey()` projection for v1 compatibility is simpler and directly satisfies the acceptance criterion. Note: baseline comparison has no existing SARIF output or Testing API surface (SARIF today is produced only by `validate` for ordinary violations; the Testing API only accepts a baseline path via `WithBaseline`, it does not expose diff/verify results) — extending either to baseline comparison results is out of scope for this change and is called out explicitly rather than silently assumed.

### 2. Document versioning: dispatch, not hard gate

`ArchitectureBaselineDocument.Version` becomes a dispatch value instead of an equality assertion:
- `1`: `ignored_violations` entries carry only `source_type`/`forbidden_reference`/`reason` (current shape, unchanged). Matching uses the existing exact string-pair key. This is loaded and matched with **zero behavior change** — the acceptance criterion "existing baseline files retain explicit legacy behavior" is satisfied by literally not touching v1 matching semantics.
- `2`: `ignored_violations` entries carry the full `ArchitectureViolationIdentity` fields (serialized with the same `UnderscoredNamingConvention` as today) plus `reason`. `source_type`/`forbidden_reference` remain present as human-readable display fields for continuity/diffability but are not consulted for matching.

`ArchitectureBaselineLoadingService.ValidateBaseline` becomes a `switch` on `Version` (1 or 2), throwing the same "unsupported version" error for anything else (3+ or 0) as it does today for "not 1" — this keeps the failure mode identical for genuinely unsupported files.

Rejected alternative: a document-level `identity_version` field independent of `version`, allowing mixed v1/v2 entries in one file. Rejected as unnecessary complexity — the issue asks for a migration *command* that produces a new file, not in-place mixed-version files; one document version cleanly gates one entry shape.

### 3. Comparer/generator/merge become version-aware

- `ArchitectureBaselineComparer.Compare` inspects the loaded document's `Version`. For `1`, `BuildComparisonKey`/`HasMatchingCandidate` are unchanged (legacy path, candidates projected down to `(SourceType, ForbiddenReference)` via a new `ArchitectureBaselineCandidate.ToLegacyPair()` helper). For `2`, matching is full `ArchitectureViolationIdentity` structural equality (record equality — all fields, including `Occurrence`).
- `ArchitectureBaselineGenerator.BuildFromEntries` always builds v2 output (new baselines are generated with full structured identity from day one); the old flat-pair dedup key is replaced by `ArchitectureViolationIdentity` equality (record `Equals`), which correctly keeps distinct occurrences separate instead of collapsing them.
- `ArchitectureBaselineLoadingService`'s merge/dedup (used by `validate --baseline`) becomes version-aware the same way: v1 baseline merged into policy ignores keeps exact-pair dedup; v2 baseline merge compares structured identity.

### 4. Candidate collection carries full identity from the start

`ArchitectureContractExecutionContext.IsIgnored` gains an overload/parameter accepting an optional `ArchitectureViolationIdentity` (or identity-building delegate) alongside the existing two strings; call sites that can supply richer data pass it, call sites that cannot fall back to a v2-shaped identity built from just `(family, contractId, sourceType, forbiddenReference)` with null assembly/member fields. This keeps the choke-point contract backward compatible for the ~20 families not touched by this change, while the two qualified families (dependency-style checks in `ArchitectureAnalysisSession.Checking.cs`, method-body scanning in `ArchitectureSourceScanner`) pass the fully populated identity.

- Dependency-style checks: add `sourceAssembly`/`targetAssembly` (already available from the `INamedTypeSymbol`/`ITypeSymbol` being checked — `ContainingAssembly.Name`) to the identity.
- Method-body/call checks: add `sourceAssembly` (containing assembly of the scanned type), `targetAssembly` (`symbol.ContainingAssembly?.Name`), `targetMember` (the existing `symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)` value, moved from being embedded in `forbidden_reference` to its own field), and `occurrence` (an ordinal counter, scoped per `(sourceType, targetMember)` within one file's scan, incremented for each additional match — not derived from the line number). The line number is retained only for human-readable display (SARIF/human output), never for identity.

### 5. `baseline migrate` command

New `IArchitectureBaselineApplicationService.Migrate(BaselineMigrateRequest) -> BaselineMigrateOutcome`, following the existing `Generate`/`Update`/`Prune`/`Diff`/`Verify` pattern:
1. Load the source (v1) baseline.
2. Run the same `CollectCandidates` flow as `generate` (execute contracts against current source) to get current v2-identity candidates.
3. For each v1 `ignored_violation`, scoped to its contract id: find current candidates whose legacy-projected `(SourceType, ForbiddenReference)` equals the entry's pair.
   - Exactly one match → emit a v2 entry using that candidate's full identity, `reason` preserved verbatim.
   - Zero matches → classify `stale`, excluded from output, listed in the report.
   - More than one match → classify `ambiguous`, excluded from output, listed in the report; a non-dry-run migration with any ambiguous entries exits non-zero and does not write `--output` (fail-closed — "refuse silent broadening").
4. `--dry-run`/`--check` runs steps 1-3 and reports only; never writes a file, always exits according to whether ambiguity/errors were found (non-zero if any ambiguous entries, for CI gating).
5. `--output` is required for a real (non-dry-run) run; the command refuses to run without it and refuses to accept a value equal to the resolved `--baseline` input path (never overwrites the source).

CLI wiring: new `MigrateBaselineSubcommandModule : IBaselineSubcommandModule` in `src/ArchLinterNet.Cli/Commands/Baseline/` (auto-discovered by the existing reflection-based `BaselineSubcommandCatalog`, no manual registration needed) + `BaselineMigrateCommandHandler`, reusing `BaselineOptionsFactory` for shared options (`--policy`, `--mode`) and adding `--baseline` (required), `--output` (required unless `--dry-run`), `--dry-run`/`--check` (aliases), `--json`.

### 6. Structured status fields

`ArchitectureBaselineComparisonResult`'s existing four buckets (`New`, `Frozen`, `Resolved`, `ConfigurationErrors`) are kept as the internal shape (no breaking change to `ArchitectureBaselineComparer`'s public result type), but the CLI JSON formatters for `diff`/`verify` (and the new `migrate`) now attach an explicit `status` string (`new`/`matched`/`stale`/`configuration_error`) to every serialized entry based on which bucket it came from, instead of requiring consumers to infer status from which JSON array an entry appears in. `Ambiguous` is new and is only produced by `migrate` (diff/verify against a well-formed baseline cannot itself observe migration ambiguity, since each baseline entry already identifies at most one candidate by construction post-migration).

## Risks / Trade-offs

- **[Risk] Threading identity through the choke point touches widely-used code** (`IsIgnored` call sites). → Mitigation: additive overload with a default fallback keeps the ~20 untouched families compiling and behaviorally unchanged; only the two named families are edited to pass richer data.
- **[Risk] `Occurrence` ordinal depends on scan order being stable.** → Mitigation: the scanner already processes syntax nodes in a deterministic (source-order) walk today (required for today's deterministic-output guarantee); the occurrence counter reuses that same deterministic order, so generated baselines remain byte-identical across repeated runs on unchanged code (existing "Deterministic output across repeated runs" scenario continues to hold).
- **[Risk] Migration ambiguity may be common for coarse legacy entries** (e.g. a baseline entry that matches many current call sites). → Mitigation: this is the intended fail-closed behavior per the issue ("refuse silent broadening", "report ambiguity") — ambiguous entries require manual review/splitting, they are not silently dropped or merged.
- **[Trade-off] Non-qualified families keep less-precise identity in this change.** → Explicitly called out as non-goal/follow-up rather than silently implied as "fixed everywhere."

## Migration Plan

1. Ship `ArchitectureViolationIdentity`, version-aware loader/comparer/generator, and qualified dependency/method-body identity, all backward compatible with existing v1 files (no action required from adopters who don't upgrade).
2. Ship `baseline migrate` as an opt-in command; adopters run it explicitly, review the `stale`/`ambiguous` report, resolve ambiguity manually (e.g. split a coarse legacy entry into several via targeted `baseline generate --contract`), then re-run `migrate --dry-run` until clean, then a real run.
3. No automatic migration is triggered by `validate`, `generate`, `update`, `prune`, `diff`, or `verify` — files stay at whatever version they're at until an adopter explicitly runs `migrate`.
4. Rollback: since `migrate` never overwrites the source file, rollback is simply continuing to use the original v1 file (or discarding the `--output` file) — no destructive step exists in this workflow.

## Open Questions

None blocking — the P0 scope (dependency + method-body qualification, migrate command, versioned schema/docs) is decided above; broader per-family qualification is deferred as documented in Non-Goals.
