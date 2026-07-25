# 0.5.1 Adoption-Stabilization Consistency Audit

This is the current-state audit performed while publishing the #355 compatibility contract. It is not the final Checkpoint B audit: that pass is intentionally deferred until all applicable design slices have landed.

Normative target: `openspec/specs/adoption-stabilization-compatibility/spec.md`.

## Result

The release-level architecture is coherent enough to become the shared source of truth. One already-shipped specification conflict, two design-source defects, and one normative coverage gap were corrected in this change. Remaining differences are implementation gaps explicitly owned by open child issues; they block #355 closure and the 0.5.1 release gate, not publication of the design contract.

## Corrected during this pass

### Baseline identity version

`openspec/specs/baseline-generation/spec.md` showed `identity_version: 2` in its v2 YAML example, while all authoritative shipped evidence uses violation identity version 1:

- `schema/baseline.schema.json` describes baseline document v2 with `identity_version` as a required structured identity field;
- `ArchitectureViolationIdentity.CurrentVersion` and loading/matching code use version 1;
- #357's implementation and migration behavior use baseline document version 2 plus violation identity version 1.

The example is corrected to `identity_version: 1`, and the archived change contains a matching `MODIFIED` delta.

```text
baseline document version = 2
violation identity version = 1
```

### Report routing does not reuse artifact output

The first draft reused `--output <format>=<destination>` for multi-sink validation reports. Existing baseline and other artifact-producing commands already own `--output <path>`. Reusing it would create two incompatible meanings in the same CLI.

The approved validation-only syntax is now repeatable `--report <format>=<destination>`. Existing validation `--format`/`--json` remain one-sink compatibility forms, while command-specific `--output <path>` remains an artifact destination.

### Multi-file output is not falsely transactional

The first draft implied an all-or-none commit for multiple independent report files. No portable filesystem primitive can guarantee that across arbitrary paths or filesystems.

The corrected contract stages and validates every file first, uses atomic same-directory replacement per destination where supported, and reports typed `partial-output` evidence if a later replacement fails after an earlier one committed. It never claims global rollback or reruns validation.

### Shipped policy-expression families are normative inputs

The first draft listed FrameworkReference and composition-root work only in the ownership matrix. That was insufficient for #355 because a future source-set or normalized-finding implementation could still weaken their already-shipped identity/evidence model.

The compatibility spec now explicitly preserves:

- FrameworkReference as a first-class family rather than a fake package;
- explicit/SDK-implicit classification, project, configuration, TFM, declaration evidence, contract identity, provenance, and fail-closed project/MSBuild evaluation;
- assembly-qualified composition source/member/project/occurrence identity;
- distinct identities for same-named roots in different assemblies;
- the non-goals of runtime DI inspection, application execution, and semantic data-flow analysis;
- small-policy defaults when these families and source sets are unused.

## Already aligned on main

### CLI status compatibility

`CliExitCodes` already exposes exactly 0/1/2. The compatibility contract preserves these numeric categories and reserves typed machine-readable completion categories for finer distinctions, including `output-failed`, `partial-output`, and `cancelled`.

### Baseline compatibility

The shipped baseline schema reads document versions 1 and 2, keeps v1 legacy semantics, requires structured fields for v2, and uses occurrence-aware exact matching. This matches the release-level compatibility model after the example correction above.

### Analysis/build-state authority

`analysis-build-state/v1` already separates build inputs, analysis inputs, verified artifacts, completed sessions, and process-local snapshot ownership. #355 reuses it rather than defining another fingerprint or cache key model.

### FrameworkReference and composition foundations

The shipped framework-reference capabilities already use real per-TFM/configuration project evaluation, preserve explicit/implicit and project evidence, and fail closed when trustworthy metadata is unavailable. The shipped composition capability already carries assembly-aware identity and typed evidence. #355 treats those capabilities as authoritative and constrains later expansion/reporting work not to weaken them.

### Policy schema compatibility

The current root policy remains `version: 1`. New source-set, optional-input, and policy-only capabilities must be additive or explicitly versioned; a small existing policy must not acquire mandatory large-solution configuration.

### Internal/public documentation boundary

The compatibility blueprint remains under `docs/internal/` and is intentionally excluded from the public product site until child features are implemented and public guidance is accurate.

## Open reconciliation owned by child slices

### #363 — immutable analysis snapshot

Must publish one immutable normalized result with explicit ownership, successful/full-graph completion, compatible requested views, disposal semantics, and reuse governed by `analysis-build-state/v1`. It must not create a second session identity model.

### #364 — multi-sink validation reports

Must implement repeatable `--report <format>=<destination>`, retain legacy one-sink validation `--format`/`--json`, preserve command-specific artifact `--output <path>`, render from one normalized result, and prove that sink count does not increase policy loads, project evaluations, scans, or contract executions. File destinations use pre-staging, per-file atomic replacement where supported, and honest `partial-output` evidence rather than a false global transaction.

### #365 — verified cache

Must use `analysis-cache/v1`, remain opt-in, treat restored bytes as untrusted, add workspace/trust/integrity authorization beyond fingerprint equality, and degrade safely to recomputation.

### #366 — acceptance and release gate

Checkpoint A remains internal evidence. Checkpoint B must execute the complete upgrade, greenfield, small, large, CLI, Testing, generic CI, offline, non-TTY, sequential, platform, security, and migration matrix before release authorization.

### #367 — migration and entrypoints

Must document one coherent policy/baseline/API/schema/report/status model for POSIX, PowerShell, generic CI, and Testing without making GitHub Actions normative.

### #368 / #369 — optional inputs and deterministic expansion

Must share exact contract/input/source-instance identity, provenance, zero-match behavior, overlap normalization, and the include-minus-exclude selector algebra. Expansion must not silently enlarge the analysis graph or weaken shipped FrameworkReference/composition identity and evidence.

### #370 — safe baseline authoring

Must align generate/migrate/update/prune/diff/verify on exact identity, preview, atomic writes, reviewed metadata preservation, and the shared lifecycle vocabulary: `new`, `matched`, `resolved`, `stale`, `changed`, `ambiguous`, and `configuration-error`. The current shipped use of `stale` for resolved debt and implementation text that still describes legacy tuple comparison are transitional and must be explicitly migrated/reconciled by this slice.

### #371 — assembly-free policy tooling

Must clearly separate checks completed from policy alone from typed deferred assembly/project checks. It must not require restore/build or claim semantic data-flow/runtime DI validation.

### #372 — packaged schemas

Must create the immutable 0.5.1 schema registry, release-qualified `$id` values, package resources, offline discovery, and digest/version validation. Current unversioned web `$id` values are convenience/current aliases, not the final release source of truth.

### #373 — typed finding details

The current `diagnostics-model` is a useful typed-subtype foundation but is not yet the complete `finding/v1` release envelope. #373 must reconcile the legacy fixed-kind wording, complete the discriminated typed-details union, preserve canonical identity/provenance, and guarantee human/JSON/SARIF/Testing parity without message parsing.

The existing `diagnostics-model` Purpose is also still a `TBD` archive placeholder and must be corrected as part of that reconciliation or the final max pass.

### #374 — profiling

Must publish `analysis-profile/v1`, deterministic counters and stable phase names, then capture pre-cache/pre-parallel and corresponding post-change checkpoints. Timing remains evidence only.

### #375 — bounded concurrency and cancellation

Must preserve sequential/parallel semantic equivalence, implement the approved default maximum parallelism, propagate cancellation across every phase, and prevent partial publication from being reported as successful.

### #94 — public API snapshots

Must implement `api-snapshot/v1` capture/diff/update/exact semantics with structural assembly/type/member identity, deterministic ordering, explicit overwrite, and atomic replacement per snapshot destination.

## Known implementation limitations that remain visible

The already-merged #362 implementation documents narrower v1 fingerprint coverage, receipt-only freshness, structural `--no-restore` prerequisite depth, and same-process assembly reload staleness. These are honest implementation limitations, not permission for downstream tasks to redefine the normative identity model. The final Checkpoint B pass must either resolve, explicitly scope, or defer them without overstating 0.5.1 guarantees.

## Current gate status

| Gate | Status |
|---|---|
| Release-level compatibility source | defined |
| Analysis/build-state slice | approved and implemented with documented limitations |
| FrameworkReference/composition compatibility | explicit and authoritative |
| Shipped baseline identity-version consistency | corrected |
| CLI report/artifact option namespace | reconciled |
| Multi-file commit semantics | made implementable and honest |
| Child slice ownership | mapped |
| OpenSpec strict validation | pending CI/local execution |
| Repository acceptance | pending CI/local execution |
| Checkpoint A walkthrough | pending applicable implementation evidence |
| Final max consistency pass | blocked until all slices land |
| Checkpoint B / 0.5.1 release authorization | blocked |

## Final-pass rule

Do not incrementally declare #355 consistent after each child PR. Each child should validate against its applicable slice, but #355 closes only after one max-depth repository-wide pass compares:

- all applicable OpenSpec capabilities and archived designs;
- internal blueprints;
- packaged schemas and compatibility manifest;
- code-level identities and public models;
- CLI help, exit/status/report/artifact behavior, and Testing API;
- migration and public documentation;
- capability claims;
- issue wording and dependency maps;
- the complete #366 Checkpoint B corpus.

Any incompatible version, identity field, lifecycle status, ownership rule, report syntax, artifact-option meaning, cache trust rule, phase name, cancellation boundary, or support claim blocks closure and release.
