# 0.5.1 Adoption-Stabilization Compatibility Blueprint

This internal blueprint is the integration reference for issue #355 and the final compatibility pass over story #354. The normative requirements live in `openspec/specs/adoption-stabilization-compatibility/spec.md`.

It does not implement all child features. It defines the boundaries that every child must consume so identity, snapshots, diagnostics, cache, schemas, output, profiling, cancellation, and compatibility do not drift into parallel models.

## Core rule

ArchLinterNet 0.5.1 is one public release contract.

Design slices may be approved and implemented independently, but the final release requires one max-depth consistency pass over all slices and their executable evidence. Checkpoint A is internal evidence only. Checkpoint B is the sole public release gate.

## Contract hierarchy

```text
adoption-stabilization/v1
├── policy-root/v1 + policy-fragment/v1
├── baseline/v2 + violation identity_version 1
├── api-snapshot/v1
├── finding/v1
├── analysis-build-state/v1
├── analysis-cache/v1
├── analysis-profile/v1
└── packaged 0.5.1 schema registry
```

The hierarchy is compositional, not substitutive:

- `analysis-build-state/v1` owns build/analysis/artifact/session identity and snapshot publication.
- `finding/v1` owns reportable result semantics and typed details.
- baseline v2 references canonical finding identity; it does not invent another key.
- API snapshot v1 owns public API exactness; it does not reuse display signatures as identity.
- cache v1 consumes completed-session identity plus independent trust and integrity controls.
- profiling v1 measures work but never changes identity.
- report adapters consume one normalized result and never re-run analysis.
- the packaged registry maps every public document to its exact 0.5.1 schema resource.

## Design-slice map

| Slice | Authoritative issue/capability | Primary consumers |
|---|---|---|
| Release compatibility and schema registry | #355 / `adoption-stabilization-compatibility` | #366, #367, #372 |
| Policy selector algebra | #355 plus shipped #356 behavior | #368, #369 |
| Violation identity and baseline migration | #357 / baseline v2 | #121, #360, #370, #373 |
| Package/framework/composition typed evidence | #358, #359, #360 | #364, #373 |
| Analysis/build state fingerprints | #387 / `analysis-build-state-fingerprints` | #362, #363, #365, #374, #375 |
| Immutable analysis snapshot | #363 | #364, #365, #374, #375 |
| Multi-sink validation reports | #364 | #366, #367 |
| Verified cache | #365 | #366, #374, #375 |
| Acceptance and release gate | #366 | all slices |
| Migration and entrypoints | #367 | all public surfaces |
| Optional planned-empty inputs | #368 | #369, coverage |
| Reusable source sets/expansion | #369 | package/framework/external/project/composition families |
| Safe baseline authoring | #370 | #121, migration docs |
| Policy-only validation | #371 | editor/pre-commit/offline workflows |
| Packaged schemas | #372 | every persisted public document |
| Typed diagnostic details | #373 | human/JSON/SARIF/Testing/baseline |
| Profiling harness | #374 | #365, #375 |
| Bounded concurrency/cancellation | #375 | snapshot/cache/output/release gate |
| Public API snapshots | #94 | #372, #366, #367 |

## Release and version registry

### Product boundary

- Public release: `0.5.1`.
- Compatibility envelope: `adoption-stabilization/v1`.
- No public Checkpoint A version.
- A later equality or required-shape change requires a new logical/document version.

### Schema resources

0.5.1 packages must contain an immutable manifest and release-qualified schemas under an equivalent of:

```text
schema/0.5.1/
  compatibility-manifest.json
  dependencies.arch.schema.json
  dependencies.arch.fragment.schema.json
  baseline.schema.json
  api-snapshot.schema.json
  finding.schema.json
  analysis-build-state.schema.json
  analysis-cache.schema.json
  analysis-profile.schema.json
```

The manifest records:

- product version;
- logical schema id and document version;
- packaged resource path;
- immutable `$id`;
- SHA-256 digest;
- read/write support;
- deprecation/migration note;
- owning OpenSpec capability.

Unversioned schema URLs may point users to the current release, but tooling must be able to resolve the release-qualified packaged copy without network access.

## Compatibility matrix

| Existing surface | 0.5.1 behavior |
|---|---|
| Policy `version: 1` | preserved unless a documented correctness fix applies |
| Single-source contracts | remain valid; no source-set configuration required |
| Validation `--format <format>` / `--json` | preserved as one legacy report sink |
| Command-specific `--output <path>` | preserved as artifact destination, never report routing |
| New validation `--report <format>=<destination>` | additive, repeatable report routing |
| Baseline `version: 1` | read with legacy semantics; never silently reinterpreted |
| Existing baseline v2 | exact current fields remain valid; newly qualified families use reviewable update/prune |
| Existing CLI exit codes | remain 0/1/2 |
| Human output without color | remains complete |
| Ordinary validation | never builds/restores implicitly |
| Existing no-cache behavior | remains default unless cache is explicitly selected |

A correctness fix may intentionally surface debt that was previously conflated. Such a fix requires explicit migration guidance and acceptance fixtures; compatibility does not mean preserving a bug.

## Stable identity versus evidence

### Canonical finding identity

Stable identity is family-specific but uses one shared model:

```text
identity_version
result kind / contract family
authored contract id
concrete source-instance key (when expanded)
source project/assembly/type/member
target project/assembly/type/member
configuration / TFM where semantically relevant
deterministic occurrence
```

Not every field is populated for every family. A field is included only when it distinguishes semantically separate findings for that family.

### Never stable identity

The following are evidence or presentation only:

- human message/reason text;
- rendered selector or expanded display label;
- absolute path;
- line/column;
- timestamps and file size;
- timings and allocation/resource metrics;
- report destination;
- CI provider;
- TTY/color/hyperlink state;
- process-local object handles;
- searched candidate paths;
- local command rendering.

Two global `Program` types in different assemblies and two same-API calls in one source member must remain distinct without using source line numbers.

## Policy expression boundary

### Include-minus-exclude

Compatible selector consumers share one set algebra:

```text
effective = canonical(include) - union(canonical(exclude))
```

No family gets an unrelated exclusion language. Existing selector forms remain valid.

### Source-set expansion

A reusable set/template has:

- authored identity and provenance;
- bounded selection over already configured analysis inputs;
- deterministic canonical expansion;
- a separate source-instance identity;
- visible zero-match/overlap/stale states;
- no implicit enlargement of `analysis.target_assemblies` or project graph.

An implementation may offer explicit lists, named sets, and constrained globs, but they must compile into this one expansion seam.

### Optional-empty lifecycle

Optionality belongs to one exact contract input, not to a whole contract or layer globally. It requires a reason and typed provenance.

Lifecycle:

```text
unknown/stale -> configuration finding
empty + no declaration -> empty-input debt
empty + exact optional declaration -> optional-empty
populated -> ordinary coverage/evaluation
```

Optional-empty state is not a baseline entry and must not suppress real violations after the input becomes populated.

## Baseline and API snapshot lifecycle

### Baseline v2

- document `version: 2`;
- finding `identity_version: 1`;
- writer output deterministic;
- v1 reader preserved;
- migrate is explicit and fail-closed on ambiguity;
- update/prune are previewable and atomic per destination file;
- reviewed reasons/metadata survive when safe round-trip is supported;
- CI verifies but does not auto-approve debt.

Lifecycle status semantics:

| Status | Meaning |
|---|---|
| `new` | current finding has no exact baseline entry |
| `matched` | baseline entry and finding have equal canonical identity |
| `resolved` | valid/evaluable baseline identity has no current finding |
| `stale` | contract, family, source instance, schema, or identity form is no longer valid/evaluable |
| `changed` | deterministic predecessor/successor exists but identity differs; no suppression until review |
| `ambiguous` | multiple candidates exist and the tool refuses to guess |
| `configuration-error` | malformed, unsupported, or inconsistent input prevents safe classification |

`changed`, `stale`, `ambiguous`, and `configuration-error` never silently suppress current findings.

### API snapshot v1

API identity is structural:

```text
assembly -> namespace -> containing type chain -> type/member kind
generic arity + signature types + relevant modifiers
```

Capture writes a complete candidate. Diff is read-only. Update is explicit and atomic for the snapshot destination. Exact validation uses canonical identity, not display strings.

## Normalized finding model

`finding/v1` is the semantic source for every adapter.

Minimum envelope:

```json
{
  "schema": "finding/v1",
  "tool_version": "0.5.1",
  "result_kind": "violation",
  "severity": "error",
  "rule_id": "strict_framework_dependency",
  "identity": {},
  "contract": {},
  "details": {
    "kind": "framework_reference"
  },
  "locations": [],
  "baseline_status": null,
  "message": "..."
}
```

Rules:

- `details.kind` is a closed discriminated union for v1.
- Common fields contain only truly common semantics.
- Family-specific evidence stays typed.
- policy root/fragment/import provenance uses consistent terminology.
- human, JSON, SARIF, Testing, explain, and baseline are projections.
- adapters may omit presentation-only fields, but not required typed evidence.
- SARIF locations/properties never become the identity source.

## Analysis snapshot and build state

`analysis-build-state/v1` is reused without modification.

Important separation:

```text
build inputs -> artifact freshness
build inputs + policy/request -> analysis identity
verified artifact bytes -> exact artifact set
analysis identity + artifact set -> completed session
completed session + local owned object -> snapshot instance
```

Consequences:

- policy-only changes do not make assemblies stale;
- configuration and TFM always distinguish identity;
- ordinary validation never builds/restores;
- explicit preparation is caller-controlled;
- a cancelled/failed/partial snapshot is never reusable;
- cache/profile/reporting may observe a session but cannot redefine it.

Known implementation limitations from child tasks must remain documented as limitations, not silently weaken this normative model.

## CLI reporting and status

### Exit codes

```text
0 success / gate passed
1 completed gate failed
2 command incomplete or cancelled
```

Typed machine status explains the category, including `output-failed` and `partial-output`. Adding many numeric codes in 0.5.1 would break existing shell usage and is unnecessary.

### Multi-sink validation report syntax

```text
--report human=stderr
--report json=artifacts/architecture.json
--report sarif=artifacts/architecture.sarif
```

Validation `--format json` and `--json` remain compatible one-sink forms. They cannot be combined with `--report`. Existing command-specific `--output <path>` options continue to create artifacts such as baselines or API snapshots and are never interpreted as report routing.

Processing:

1. produce one immutable normalized result;
1. sort and baseline-classify once;
1. render each requested report;
1. validate bounded output;
1. stage every file report in its destination directory before changing any destination;
1. atomically replace each destination, where supported, in deterministic order;
1. if a later replacement fails, report typed `partial-output` evidence listing committed and uncommitted destinations without claiming global rollback;
1. return one status without re-analysis.

There is no portable all-or-none transaction across independent report paths or filesystems. A pre-commit render/validation failure changes no destination; a mid-commit replacement failure is incomplete execution, exits 2, and exposes the exact partial state. Standard streams are also not transactional. Conflicting stream/path destinations and input overwrite are rejected before commit.

## Cache contract

### Default

Persistent cache is opt-in:

```text
(no cache option) -> disabled
--cache auto -> platform user cache/ArchLinterNet/0.5.1/analysis-cache/v1
--cache <path> -> trusted caller-selected location
```

This keeps small policies simple and avoids surprising repository writes.

### Authorization and integrity

Fingerprint equality is not authorization.

A cache entry also requires:

- cache schema/tool versions;
- workspace/trust-domain scope;
- requested compatible views;
- content digest/integrity;
- containment/path safety;
- successful completed-session status;
- no cancellation/partial marker.

Each entry's tag is an HMAC with a per-cache-root local secret stored in a sibling authentication
namespace, never beneath the cache root. Generic CI cache configuration may restore the cache root
as untrusted optimization data, but it must exclude that sibling authentication namespace so an
archive cannot replace both an entry and the key that validates it. Corrupt, foreign,
incompatible, or poisoned content causes verified recomputation. CI caches are optimization
artifacts, never trusted correctness evidence.

## Profiling and optimization checkpoints

`analysis-profile/v1` records:

- deterministic counters;
- phase timings;
- optional bounded resource measurements;
- cache hit/miss/read/write;
- cancellation observation;
- tool/runtime/fixture metadata needed to interpret a run.

It does not enter finding/session identity.

#374 must record:

1. pre-cache/pre-parallel checkpoint;
1. post-cache checkpoint;
1. post-parallel/cancellation checkpoint.

Acceptance gates schema and counters. Wall-clock numbers inform decisions but are not universal pass/fail thresholds.

## Concurrency and cancellation

Default maximum parallelism:

```text
max(1, min(Environment.ProcessorCount, 4))
```

`--max-parallelism 1` is supported. Higher positive values remain bounded by caller intent and implementation safety.

Parallel execution must equal sequential execution in:

- canonical finding set;
- identity;
- baseline status;
- ordering;
- output schema;
- exit category.

Cancellation crosses every phase. It wins if observed before successful publication or commit. No partial snapshot/cache/profile/baseline/API snapshot/report set may be presented as successful.

## Policy-only tooling

Policy-only validation performs:

- root/fragment schema validation;
- secure import resolution;
- composition;
- static entity/reference checks;
- selector/set/optional-input syntax and identity;
- deferred-check inventory.

It does not require:

- target assemblies;
- restore/build;
- DI-container inspection;
- application execution;
- semantic data-flow analysis.

Assembly/project-dependent checks are typed `deferred`, not passed.

## Support and evidence matrix

Checkpoint B must include representative executable evidence for:

| Audience/environment | Required evidence |
|---|---|
| 0.5.0 upgrade | policy compatibility, baseline migration/update, format guidance |
| Greenfield small project | minimal config and no opt-in large-solution features |
| Ordinary multi-project | project/TFM/config identity and multi-sink reporting |
| Large multi-host | same-named types, expansion, bounded concurrency, cache/profile |
| CLI | Bash/POSIX and PowerShell status/argument forwarding |
| Generic CI | non-TTY, no provider-specific semantics |
| Testing API | snapshot ownership, typed findings, cancellation/disposal |
| Offline prepared checkout | packaged schemas, no-restore, no network |
| Resource constrained | sequential mode |
| Every claimed OS/architecture | policy loading/filesystem/schema/non-TTY smoke |

## Security checklist

Every child implementation must consider:

- path traversal, case aliases, symlinks/junctions;
- malicious policy/baseline/snapshot/cache/receipt/schema content;
- command/argument injection;
- policy-controlled execution attempts;
- report/artifact overwrite and disclosure;
- cache poisoning/cross-workspace reuse;
- TOCTOU between verification and use;
- oversized/deep documents and output;
- secret/environment leakage.

Executable/argv data comes only from trusted caller configuration. Never from policy or persisted analysis artifacts.

## Final max consistency pass

The final #355 pass is deliberately one max-depth review, after slice work has landed.

Review inputs:

1. this spec and blueprint;
1. all child OpenSpec capabilities;
1. archived design histories;
1. `schema/0.5.1` registry/resources;
1. capability manifest;
1. CLI help and Testing API;
1. migration/public docs;
1. #366 Checkpoint B scenarios;
1. issue descriptions and dependency wording.

Review questions:

- Is there exactly one identity model?
- Is there exactly one snapshot/build-state model?
- Is every diagnostic family represented by `finding/v1` typed details?
- Are baseline/API/cache/profile versions unambiguous?
- Does every report sink consume one result?
- Are `--report` and artifact `--output` unambiguous?
- Are per-file atomicity and partial multi-file failure reported honestly?
- Do exit codes remain 0/1/2?
- Is cache opt-in and untrusted?
- Is sequential mode supported?
- Can cancellation publish nothing partial as successful?
- Are small defaults still small?
- Are platform/offline/non-TTY claims executable?
- Does every child reference the applicable slice?
- Are known limitations explicit and scheduled, rather than hidden?

Any incompatible answer blocks #355 closure and Checkpoint B release authorization.
