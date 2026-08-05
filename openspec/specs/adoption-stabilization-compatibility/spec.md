# Adoption Stabilization Compatibility Specification

## Purpose

Define the single, versioned compatibility and architecture contract for ArchLinterNet 0.5.1 across policy authoring, exact identity, baselines, API snapshots, diagnostics, build state, output, cache, profiling, cancellation, packaged schemas, and supported execution environments.
## Requirements
### Requirement: One public 0.5.1 release boundary
The system SHALL expose one public stabilization release boundary, `0.5.1`,
governed by compatibility envelope `adoption-stabilization/v1`. Checkpoint A is
internal integration evidence only; only a complete, reproducible Checkpoint B
packed-artifact acceptance matrix and release-evidence summary may authorize
publishing 0.5.1.

#### Scenario: Checkpoint A succeeds
- **WHEN** the safety-critical Checkpoint A scenarios pass
- **THEN** development may continue but no intermediate public stabilization
  version is implied or authorized

#### Scenario: Unrelated design slices progress independently
- **WHEN** one child task has an approved applicable design slice and another
  unrelated slice remains open
- **THEN** the first task may proceed without redefining the unresolved slice or
  waiting for the complete issue to close

### Requirement: Release-matched compatibility registry
The system SHALL publish one `adoption-stabilization/v1` registry for 0.5.1 that identifies every shipped persisted or machine-readable contract by logical schema id, document version, packaged resource path, and compatibility behavior. The 0.5.1 registry SHALL contain exactly the formats whose owning slices implement their writers and validate real generated output:

| Surface | Logical schema/version | 0.5.1 writer behavior |
|---|---|---|
| Root policy | `policy-root/v1`, YAML `version: 1` | writes/validates v1 |
| Imported fragment | `policy-fragment/v1` | writes/validates the release-matched fragment schema |
| Baseline | `baseline/v2`, YAML `version: 2`, identity `identity_version: 1` | writes v2; reads v1 and v2 |
| Public API snapshot | `api-snapshot/v1`, document `version: 1` | writes v1 |
| Normalized finding | `finding/v1`, JSON `schema_version: 1` | writes v1; unknown schema versions fail and unknown v1 kinds follow the documented strict/non-strict rule |
| Analysis/build state | `analysis-build-state/v1` | reuses the approved fingerprint/receipt contract |
| Analysis cache | `analysis-cache/v1`, envelope format version 2 | writes and inspects verified cache entries; unsupported versions fail explicitly |
| Analysis profile | `analysis-profile/v1` | writes deterministic counters and optional measurements; the package declares write-only support until a public reader exists |
| Compatibility registry | `adoption-stabilization/v1` | writes the release-matched registry |

Packaged JSON Schemas and text-format contracts SHALL use immutable release-qualified ids under `https://archlinternet.dev/schema/0.5.1/` and SHALL be shipped in the CLI and applicable NuGet packages. Unversioned web schema URLs MAY remain convenience aliases but SHALL NOT be the compatibility source of truth.

#### Scenario: Schema is consumed offline
- **WHEN** an editor, pre-commit hook, or CI job has the 0.5.1 package but no network access
- **THEN** it can discover and validate every shipped 0.5.1 document format from packaged resources and the registry

#### Scenario: Future format changes equality
- **WHEN** a future release changes a required field, equality rule, discriminated union, or canonicalization rule
- **THEN** it introduces a new logical/document version or an explicitly compatible additive extension instead of silently reinterpreting a 0.5.1 version

### Requirement: 0.5.0 compatibility and migration are explicit
The system SHALL preserve supported 0.5.0 policy meaning unless a 0.5.1 feature is explicitly opted into or a documented correctness fix requires requalification. Existing policy YAML without new fields SHALL remain valid. Baseline v1 SHALL retain its legacy exact matching semantics and SHALL never be silently reinterpreted as baseline v2. Changed baseline, API snapshot, and diagnostic formats SHALL have explicit generate, migrate, update, or recapture guidance.

#### Scenario: Existing 0.5.0 policy is loaded
- **WHEN** a supported 0.5.0 policy uses no removed or newly invalid construct
- **THEN** 0.5.1 preserves its policy meaning and does not require configuration for unused large-solution features

#### Scenario: Legacy baseline is used
- **WHEN** validation reads a baseline with `version: 1`
- **THEN** it applies the legacy identity exactly and recommends explicit migration rather than widening or silently upgrading matches

#### Scenario: Previously under-qualified identity becomes exact
- **WHEN** a 0.5.1 correctness fix adds assembly, member, configuration, TFM, or occurrence qualification to an already-v2 finding family
- **THEN** migration guidance uses a reviewable update-then-prune or recapture workflow and does not claim that an unrelated format migration can infer missing identity

### Requirement: Stable CLI exit-code categories
The CLI SHALL preserve exactly three numeric exit-code categories for 0.5.1:

- `0`: command completed successfully and the requested gate passed;
- `1`: command completed and a validation, baseline, diff, or verification gate failed;
- `2`: command could not complete because of invalid arguments, malformed or unsupported input, preparation/preflight/runtime failure, required-output failure, or cancellation.

Machine-readable output SHALL additionally expose a typed status category so callers do not need more numeric codes to distinguish `invalid-input`, `configuration-error`, `preflight-failed`, `build-failed`, `output-failed`, `partial-output`, `cancelled`, or other completion states.

#### Scenario: Architecture violation is found
- **WHEN** validation completes normally and the selected gate fails because of findings
- **THEN** the process exits `1`

#### Scenario: Cancellation is requested
- **WHEN** cancellation stops evaluation, build, hashing, analysis, rendering, or output commit
- **THEN** the process exits `2`, reports typed category `cancelled`, and never reports a clean validation result

### Requirement: Selector algebra and reusable expansion are deterministic
The policy model SHALL use one include-minus-exclude algebra for compatible selectors, preserve existing exact-source forms, and provide one reusable source-set/expansion seam rather than family-specific macro systems. Every authored set/template SHALL have stable provenance. Every concrete expansion SHALL retain the authored contract id plus a separate canonical source-instance key; display concatenation SHALL NOT become stable identity.

A zero-match selector/set SHALL fail closed unless the exact contract input is declared optional. Expansion SHALL be limited to already selected analysis projects/assemblies and SHALL NOT silently enlarge the analysis graph.

#### Scenario: Named source set expands
- **WHEN** one contract targets a named set that resolves to multiple configured projects
- **THEN** expansion order is canonical, each concrete source has distinct identity/evidence, and adding another matching project changes the visible expansion inventory

#### Scenario: Sets overlap
- **WHEN** two authored sets resolve the same concrete contract/source instance
- **THEN** the system deterministically normalizes the duplicate or rejects the ambiguity and never emits duplicate findings by accident

#### Scenario: Simple exact source remains
- **WHEN** a small policy uses the existing single-source form
- **THEN** it requires no source-set, glob, cache, profiling, or concurrency configuration

### Requirement: Shipped framework-reference and composition governance remains authoritative
The 0.5.1 compatibility envelope SHALL compose with the shipped `framework-reference-contracts`, `framework-reference-allow-only-contracts`, and `composition-contracts` capabilities without reclassifying a `FrameworkReference` as a package or reducing a composition source to display-only type text.

Framework-reference findings SHALL preserve the framework name, explicit-versus-SDK-implicit classification, source project/assembly, evaluated build configuration, target framework, declaration/project-file evidence, contract identity, and policy provenance supported by the shipped capability. Scoped project/MSBuild evaluation that cannot be completed SHALL fail closed rather than produce a clean result. Policies without framework-reference contracts SHALL retain existing behavior and require no new project-metadata configuration.

Composition findings SHALL preserve assembly-qualified source type/member identity, source project where available, semantic role/contract identity, exact occurrence, and policy provenance. Same-named composition roots in different assemblies and multiple occurrences in one source type SHALL remain distinct. Composition governance SHALL NOT require runtime dependency-injection inspection, application execution, or semantic data-flow analysis.

Reusable source-set expansion MAY feed these contract source selectors only after resolving each concrete source to an already-selected project/assembly identity. Expansion SHALL NOT weaken the family-specific exact identity or typed evidence required by the shipped capabilities.

#### Scenario: Same framework reference exists in different project contexts
- **WHEN** the same framework name is active in two projects, target frameworks, or build configurations
- **THEN** each applicable finding remains distinct by project/configuration/TFM identity and baselining one does not suppress the other

#### Scenario: Framework metadata cannot be evaluated
- **WHEN** a scoped framework-reference contract cannot obtain trustworthy project/MSBuild metadata for one source context
- **THEN** validation reports a typed fail-closed configuration/evaluation finding and does not report that context as clean

#### Scenario: Same-named composition roots exist in two assemblies
- **WHEN** two assemblies each contain a same-named `Program` or registration type covered by composition governance
- **THEN** their finding and baseline identities remain distinct by source assembly/project identity

#### Scenario: New governance families are unused
- **WHEN** a policy declares neither framework-reference nor composition contracts and uses no source sets
- **THEN** 0.5.1 preserves the existing small-policy behavior without requiring project discovery or expansion configuration solely for these capabilities

### Requirement: Planned-empty rule inputs are first-class lifecycle state
The policy schema SHALL allow an exact contract input to be declared intentionally optional with a mandatory non-empty reason and authored provenance. The declaration SHALL suppress only the matching `empty-input` coverage debt, SHALL remain visible as typed `optional-empty` state, and SHALL automatically become ordinary populated coverage when matching code appears. Stale or unknown contract/input references SHALL fail closed.

#### Scenario: Future layer is intentionally empty
- **WHEN** an exact rule input is declared optional with a reason and currently matches no code
- **THEN** coverage reports `optional-empty` for that input without weakening coverage for other inputs

#### Scenario: Matching code appears
- **WHEN** the same optional input later resolves to code
- **THEN** it is evaluated as an ordinary populated input without requiring the optional declaration to be removed first

### Requirement: Exact violation identity is occurrence aware
Every baseline-capable finding SHALL use `identity_version: 1` with stable fields appropriate to its family, including contract family/kind, authored contract id, concrete source-instance key where expansion applies, source/target project or assembly, source/target type/member, configuration, target framework, and a deterministic non-line-based occurrence discriminator where multiple otherwise-equal findings can coexist.

Display messages, reasons, rendered selectors, absolute paths, line/column, timestamps, timings, output destinations, CI provider, TTY/color state, and process-local handles SHALL NOT participate in stable identity.

#### Scenario: Same-named global Program types
- **WHEN** two assemblies each contain a global `Program` type that produces the same family of finding
- **THEN** their identities differ by source assembly/project identity

#### Scenario: Same API is called twice
- **WHEN** one source member contains two distinct calls to the same forbidden API
- **THEN** the findings differ by deterministic occurrence and baselining one does not suppress the other

#### Scenario: Output format changes
- **WHEN** the same finding is rendered as human, JSON, SARIF, or exposed through Testing
- **THEN** its canonical identity remains unchanged

### Requirement: Baseline lifecycle is safe and reviewable
Baseline writers SHALL emit `version: 2` and preserve canonical exact identity. Generate, migrate, update, prune, diff, and verify SHALL share one lifecycle vocabulary:

- `new`: a current finding has no exact baseline entry;
- `matched`: an entry and current finding have equal canonical identity;
- `resolved`: a valid, evaluable baseline identity has no current finding;
- `stale`: the entry references a contract, family, source instance, schema, or identity form that is no longer valid/evaluable, distinct from resolved debt;
- `changed`: a deterministic predecessor/successor relationship can be shown but canonical identity differs, so the entry does not suppress until explicitly reviewed;
- `ambiguous`: more than one candidate could correspond to an entry and the tool refuses to guess;
- `configuration-error`: malformed, unsupported, or inconsistent input prevents safe classification.

Existing files SHALL not be overwritten without explicit intent; update/prune SHALL preview changes and use atomic replacement. Reviewed reasons and metadata SHALL be preserved when safe round-trip is supported, otherwise the command SHALL stop with an actionable diagnostic and leave the original unchanged. `changed`, `stale`, `ambiguous`, and `configuration-error` SHALL never silently suppress a current finding.

CI guidance SHALL verify baselines but SHALL NOT automatically approve or write new debt.

#### Scenario: Baseline update fails while writing
- **WHEN** serialization, validation, or atomic replacement fails
- **THEN** the original baseline bytes remain unchanged and the command exits as incomplete

#### Scenario: Comment-preserving round trip is unavailable
- **WHEN** a file contains reviewed content that the implementation cannot safely preserve
- **THEN** update/prune refuses the write and produces a preview plus an actionable manual path

### Requirement: Public API snapshots have exact capture/diff/update semantics
Public API snapshot v1 SHALL use deterministic canonical ordering and an explicit identity for assemblies, namespaces, types, and members, including generic arity and relevant signature modifiers. `capture` SHALL write a complete candidate, `diff` SHALL be read-only, `update` SHALL require explicit overwrite and atomic replacement, and `exact` validation SHALL fail on additions, removals, or signature changes according to the selected visibility contract. Display formatting SHALL NOT be snapshot identity.

#### Scenario: Same simple type name exists in two assemblies
- **WHEN** API capture sees both types
- **THEN** their snapshot identities remain distinct by assembly and containing identity

#### Scenario: Update is interrupted
- **WHEN** snapshot update fails or is cancelled before atomic commit
- **THEN** the previous snapshot remains intact

### Requirement: One normalized finding envelope
Every reportable result SHALL project from one normalized `finding/v1` envelope containing at least:

- `schema`, `tool_version`, `result_kind`, `severity`, and stable `rule_id`;
- canonical `identity`;
- authored contract identity and policy provenance;
- a discriminated `details.kind` with family-specific typed details;
- primary and related typed locations;
- baseline/lifecycle status where applicable;
- a complete plain-text message derived from typed content.

Human, JSON, SARIF, Testing API, explain, baseline, and future automation adapters SHALL consume this envelope or an equivalent in-memory form and SHALL NOT independently reconstruct family semantics from display strings.

#### Scenario: Package or FrameworkReference finding is rendered
- **WHEN** the finding is projected to human, JSON, SARIF, and Testing
- **THEN** every projection preserves equivalent package/framework, project, configuration, TFM, explicitness, and provenance evidence supported by that finding family

#### Scenario: Unknown details kind is read
- **WHEN** a consumer encounters a future non-compatible details discriminator
- **THEN** it fails or reports unsupported schema explicitly rather than treating it as a generic dependency finding

### Requirement: Plain-text and non-TTY behavior are complete
Human output SHALL be semantically complete with ANSI color disabled and SHALL not require terminal width, hyperlinks, diagrams, or interactive prompts. Required information SHALL be available in text on POSIX, PowerShell, generic CI, and non-TTY execution. Color and hyperlinks MAY be additive presentation only.

#### Scenario: Output is redirected
- **WHEN** human output is redirected to a file or pipe with no TTY
- **THEN** it remains complete, deterministic where promised, and contains no required color-only distinction

### Requirement: Analysis/build-state identity is authoritative
All 0.5.1 tasks SHALL reuse `analysis-build-state/v1` for build-input, analysis-input, expected-output, verified-artifact, completed-session, snapshot ownership, preparation, offline, and cancellation-safe publication semantics. Cache, profiling, diagnostics, and output SHALL add their own versioned envelopes without redefining fingerprint equality or making policy-only changes mark unchanged assemblies stale.

#### Scenario: Cache consumes a completed session
- **WHEN** a cache key is constructed
- **THEN** the completed-session fingerprint is an input but not by itself authorization to trust or reuse cache content

#### Scenario: Policy changes only
- **WHEN** policy content changes but build inputs and artifacts do not
- **THEN** analysis/session identity changes while artifact freshness remains unchanged

### Requirement: Multi-sink validation report syntax and commit semantics are fixed
The root validation command SHALL accept repeatable `--report <format>=<destination>` options, where `format` is `human`, `json`, or `sarif`, and `destination` is `stdout`, `stderr`, or a caller-provided file path. Existing validation `--format <format>` and `--json` usage SHALL remain supported as one-sink legacy forms; combining either legacy form with `--report` SHALL be rejected as ambiguous. Existing command-specific `--output <path>` options used to create baseline, API snapshot, graph, or other artifacts SHALL retain their existing meaning and SHALL NOT be reused for report routing.

All report sinks SHALL consume one normalized result. Every file sink SHALL be rendered to a bounded temporary file in its destination directory and validated before the first destination is changed. Each destination SHALL then be replaced atomically where the host filesystem supports atomic same-directory replacement. Multi-file all-or-none commit SHALL NOT be claimed across independent paths or filesystems. If a replacement fails after another destination has already committed, the command SHALL report typed `partial-output` evidence identifying committed and uncommitted destinations, exit `2`, and SHALL NOT rerun validation. Input policy, baseline, snapshot, schema, and receipt paths SHALL not be overwritten. Duplicate/conflicting standard-stream destinations SHALL be rejected unless their ordering is explicitly deterministic and documented.

#### Scenario: Human plus JSON plus SARIF
- **WHEN** one validation invocation requests `--report human=stderr`, `--report json=report.json`, and `--report sarif=report.sarif`
- **THEN** policy loading, project evaluation, assembly scanning, baseline comparison, and contract execution occur once

#### Scenario: Existing artifact output option remains unambiguous
- **WHEN** a user runs `baseline generate --output baseline.yml`
- **THEN** `--output` remains the baseline artifact destination and is never interpreted as a validation report sink

#### Scenario: Rendering or validation fails before commit
- **WHEN** any required file sink fails rendering, size checks, or validation before destination replacement begins
- **THEN** no file destination is changed, the command exits `2` with `output-failed`, and validation is not rerun

#### Scenario: A later atomic replacement fails
- **WHEN** all sinks were staged successfully but replacing a later destination fails after an earlier destination committed
- **THEN** the command exits `2`, reports `partial-output` with the exact committed and uncommitted destinations, does not claim global rollback, and does not rerun validation

### Requirement: Cache defaults and trust boundary are safe
Analysis cache v1 SHALL be disabled unless the caller selects `--cache auto` or `--cache <path>`. `auto` SHALL resolve to the platform user-cache directory under `ArchLinterNet/0.5.1/analysis-cache/v1`, never inside policy-controlled content. Cache entries SHALL be scoped by workspace/trust-domain identity, completed-session fingerprint, tool/schema versions, and applicable requested views; SHALL be content-addressed and integrity checked; and SHALL be treated as untrusted until all identity, containment, and digest checks pass.

Generic CI guidance SHALL use a job-local cache directory and MAY persist it only as an optimization. A missing, corrupt, poisoned, or incompatible cache SHALL degrade to verified recomputation, not to a clean result or execution failure unless the caller explicitly requires cache-only behavior.

#### Scenario: Cache entry came from another workspace
- **WHEN** bytes have a matching session fingerprint but fail workspace/trust-domain authorization
- **THEN** the entry is ignored or quarantined and analysis is recomputed

#### Scenario: Cache is disabled
- **WHEN** the caller supplies no cache option
- **THEN** validation performs no persistent cache read or write and small-policy behavior remains simple

### Requirement: Profiling has a versioned measured-work schema
Analysis profile v1 SHALL record deterministic counters and phase measurements for policy composition, project evaluation, fingerprinting, artifact verification, snapshot materialization, contract-family execution, rendering, output commit, cache lookup/read/write, and cancellation observation where applicable. Timing and resource measurements SHALL be evidence only and SHALL not affect finding or session identity.

#374 SHALL record a pre-cache/pre-parallel checkpoint before #365 or #375 relies on optimization claims, and SHALL record corresponding post-change checkpoints. Acceptance SHALL gate deterministic counters and schema validity, not wall-clock thresholds tied to one machine.

#### Scenario: Another output sink is added
- **WHEN** multi-sink rendering is requested
- **THEN** analysis counters remain unchanged while only rendering/output counters may increase

#### Scenario: Cache optimization is evaluated
- **WHEN** pre-cache and post-cache runs use the same fixture and requested views
- **THEN** the report exposes comparable counters and phase boundaries without claiming machine-independent timing equality

### Requirement: Concurrency is bounded and sequential execution is supported
The 0.5.1 default maximum analysis parallelism SHALL be `max(1, min(Environment.ProcessorCount, 4))`. The caller SHALL be able to select `--max-parallelism 1` for supported sequential execution and MAY select another positive bounded value. Parallel work SHALL preserve the same canonical result set, identity, ordering, and exit status as sequential work.

Cancellation SHALL be cooperative across evaluation, build/preflight, hashing, scanning, contract execution, rendering, cache I/O, and output commit. No cancelled or partial session, cache entry, profile, baseline, snapshot, or required output set SHALL be published as successful.

#### Scenario: Parallel and sequential runs are compared
- **WHEN** the same immutable inputs are analyzed with maximum parallelism 1 and 4
- **THEN** canonical findings, baseline status, output ordering, and exit category are equivalent

#### Scenario: Cancellation races with completion
- **WHEN** cancellation is observed before successful publication/commit
- **THEN** cancellation wins and no reusable partial artifact is published

### Requirement: Policy-only validation is assembly free
The tool SHALL provide an assembly-free policy-check boundary that validates root/fragment schema, imports, canonical path/security rules, composition, static references between policy entities, selector/set syntax, optional-input declarations, and deferred-check inventory without requiring target assemblies, restore, build, runtime dependency-injection inspection, application execution, or semantic data-flow analysis.

Checks that require evaluated projects, assemblies, source facts, or runtime-derived evidence SHALL be reported as typed deferred checks, not silently passed.

#### Scenario: Editor validates a policy offline
- **WHEN** packaged schemas and imported policy files are locally available but assemblies are absent
- **THEN** policy-only validation completes and clearly lists deferred assembly/project checks

### Requirement: Support claims require executable evidence
The 0.5.1 support contract SHALL cover upgrade and greenfield adoption; small single-project, ordinary multi-project, and large multi-host solutions; direct CLI, generic CI, and `ArchLinterNet.Testing`; POSIX and PowerShell entrypoints; interactive and non-interactive execution; offline prepared checkouts; resource-constrained sequential mode; and every operating system and architecture publicly claimed by the release.

GitHub Actions MAY be documented as an example but SHALL NOT be required for semantics, schemas, status, artifacts, or acceptance evidence.

#### Scenario: Platform is publicly claimed
- **WHEN** 0.5.1 release metadata claims one platform/architecture
- **THEN** Checkpoint B includes executable smoke or regression evidence for policy loading, validation, packaged schemas, non-TTY output, and relevant filesystem behavior on that platform

### Requirement: Security and privacy are cross-cutting
The design and implementation SHALL threat-model repository paths and symlinks, malicious YAML/JSON/cache/receipt/snapshot content, MSBuild/project inputs, command/argument injection, output-path disclosure and overwrite, cache poisoning, decompression/resource exhaustion where applicable, and TOCTOU changes. Policy/baseline/snapshot/cache/receipt content SHALL never grant permission to execute a command or select an executable.

Machine-readable output SHALL prefer repository-relative or typed logical paths and omit absolute paths by default. Secrets, unrelated environment variables, credentials, and raw untrusted command strings SHALL not enter identity, cache keys, diagnostics, profiles, or SARIF properties.

#### Scenario: Policy attempts command injection
- **WHEN** executable or argument-like content appears in policy, fragment, baseline, snapshot, receipt, or cache data
- **THEN** it is treated only as data or rejected and is never executed

### Requirement: Final max consistency gate is mandatory
Before #355 closes and before Checkpoint B can authorize release, one repository-wide consistency pass SHALL compare this capability, every applicable child OpenSpec capability, the architecture blueprints, packaged schema registry, capability manifest, CLI/API terminology, migration documentation, and issue wording.

The pass SHALL verify at minimum that:
- no child redefines identity, snapshot ownership, finding details, output syntax, cache trust, profiling phase names, concurrency, cancellation, or exit categories;
- every child of #354 references its applicable slice or an approved subordinate slice;
- implemented limitations are represented honestly and do not weaken the normative release gate;
- the #366 small and large adopter scenarios walk through one coherent model.

#### Scenario: A child implementation diverges
- **WHEN** one task introduces an incompatible field, version, status, or ownership rule
- **THEN** #355 remains open and Checkpoint B is blocked until the child or this contract is explicitly reconciled through OpenSpec

#### Scenario: All slices are implemented
- **WHEN** the final consistency pass and #366 Checkpoint B both succeed
- **THEN** #355 may close and the 0.5.1 release may be authorized

### Requirement: Planned-empty rule inputs are implemented as schema-backed coverage state
The implementation SHALL provide the planned-empty rule-input lifecycle required by this capability's compatibility contract: exact input identity, mandatory reason, provenance, typed output, automatic covered transition, and fail-closed stale or unknown identities.

#### Scenario: Compatibility lifecycle is preserved
- **WHEN** a policy moves from a planned-empty input to matching code
- **THEN** the same declaration produces optional-empty before the code exists and covered state after it exists

### Requirement: Reusable source sets implement the 0.5.1 policy-expression design slice
The 0.5.1 compatibility contract SHALL include reusable source sets and deterministic contract expansion as an additive policy-expression capability. Policies that declare exact single sources SHALL remain valid and unchanged, and expansion SHALL NOT extend analysis beyond the declared `analysis` inputs.

#### Scenario: Existing exact-source policy is unchanged
- **WHEN** a 0.5.0 policy declares only exact `source` values
- **THEN** it loads, expands to nothing, and produces identical contract identities and findings

### Requirement: Packaged schema registry is an executable release contract
The 0.5.1 `adoption-stabilization/v1` compatibility envelope SHALL be represented by an immutable packaged schema registry. Unversioned web schema URLs MAY remain convenience aliases, but SHALL NOT be the source of truth for an installed release contract.

#### Scenario: Later source alias changes
- **WHEN** the repository default branch changes an unversioned schema alias after a 0.5.1 package is installed
- **THEN** the installed tool continues to list and print the same release-qualified 0.5.1 schema set and digests

### Requirement: Checkpoint A reusable corpus is non-release evidence
The compatibility workflow SHALL maintain one reusable synthetic adopter corpus and deterministic scenario inventory for internal Checkpoint A evidence. The corpus SHALL cover small, conventional multi-project, multi-host, legacy-migration, and clean-checkout shapes, and SHALL be reused by profiling, consistency review, and final acceptance work. Checkpoint A evidence SHALL be explicitly non-release evidence and SHALL NOT authorize package publication or version 0.5.1.

#### Scenario: Checkpoint A evidence is recorded
- **WHEN** scoped adoption-critical scenarios pass on an observed platform
- **THEN** the recorded evidence identifies the exercised scenarios and platform while retaining Checkpoint B as the only release authorization gate

### Requirement: Canonical identity requalification has an explicit baseline lifecycle
When a structured baseline identity gains required canonical qualification, documentation and
machine comparison results SHALL direct users to review `changed`, `stale`, or `ambiguous` entries
and explicitly update or recapture before pruning. The system SHALL not automatically approve,
rewrite, or broaden such entries.

#### Scenario: A user verifies a requalified baseline
- **WHEN** baseline verification encounters a structured identity that no longer exactly matches after requalification
- **THEN** verification SHALL fail with the structured lifecycle status and the user SHALL be able to follow a documented review, update/recapture, and prune path.

### Requirement: Checkpoint B authorizes exact release artifacts
Checkpoint B authorization SHALL apply only to the version-resolved package
manifest downloaded by every platform runner and re-verified by the publishing
job. A later pack, dry-run rerun, or package set with different metadata or
digest SHALL require a new Checkpoint B result.

#### Scenario: Publishing candidate differs from tested candidate
- **WHEN** a publishing job observes a candidate package digest not present in
  the successful Checkpoint B manifest
- **THEN** it fails before publication

