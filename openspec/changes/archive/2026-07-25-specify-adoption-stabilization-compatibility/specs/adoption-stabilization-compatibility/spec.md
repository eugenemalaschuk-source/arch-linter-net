## ADDED Requirements

### Requirement: One public 0.5.1 release boundary
The system SHALL expose one public stabilization release boundary, `0.5.1`, governed by compatibility envelope `adoption-stabilization/v1`. Checkpoint A is internal integration evidence only; only the complete Checkpoint B contract and acceptance corpus may authorize publishing 0.5.1.

#### Scenario: Checkpoint A succeeds
- **WHEN** the safety-critical Checkpoint A scenarios pass
- **THEN** development may continue but no intermediate public stabilization version is implied or authorized

#### Scenario: Unrelated design slices progress independently
- **WHEN** one child task has an approved applicable design slice and another unrelated slice remains open
- **THEN** the first task may proceed without redefining the unresolved slice or waiting for the complete issue to close

### Requirement: Release-matched compatibility registry
The system SHALL publish one `adoption-stabilization/v1` registry for 0.5.1 that identifies every persisted or machine-readable contract by logical schema id, document version, packaged resource path, and compatibility behavior. The 0.5.1 registry SHALL contain at least:

| Surface | Logical schema/version | 0.5.1 writer behavior |
|---|---|---|
| Root policy | `policy-root/v1`, YAML `version: 1` | writes/validates v1 |
| Imported fragment | `policy-fragment/v1` | writes/validates the release-matched fragment schema |
| Baseline | `baseline/v2`, YAML `version: 2`, identity `identity_version: 1` | writes v2; reads v1 and v2 |
| Public API snapshot | `api-snapshot/v1`, document `version: 1` | writes v1 |
| Normalized finding | `finding/v1` | writes v1 |
| Analysis/build state | `analysis-build-state/v1` | reuses the approved fingerprint/receipt contract |
| Analysis cache | `analysis-cache/v1` | writes v1 only after verification |
| Profiling report | `analysis-profile/v1` | writes v1 |
| Compatibility registry | `adoption-stabilization/v1` | writes the release-matched registry |

Packaged JSON Schemas SHALL use immutable release-qualified ids under `https://archlinternet.dev/schema/0.5.1/` and SHALL be shipped in the CLI and applicable NuGet packages. Unversioned web schema URLs MAY remain convenience aliases but SHALL NOT be the compatibility source of truth.

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

Machine-readable output SHALL additionally expose a typed status category so callers do not need more numeric codes to distinguish `invalid-input`, `configuration-error`, `preflight-failed`, `build-failed`, `output-failed`, `cancelled`, or other completion states.

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
Baseline writers SHALL emit `version: 2` and preserve canonical exact identity. Generate, migrate, update, prune, diff, and verify SHALL share one lifecycle model with typed statuses at least `new`, `matched`, `resolved`, `stale`, `changed`, `ambiguous`, and `configuration-error`. Existing files SHALL not be overwritten without explicit intent; update/prune SHALL preview changes and use atomic replacement. Reviewed reasons and metadata SHALL be preserved when safe round-trip is supported, otherwise the command SHALL stop with an actionable diagnostic and leave the original unchanged.

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

### Requirement: Multi-sink output syntax and commit semantics are fixed
The 0.5.1 CLI SHALL accept repeatable `--output <format>=<destination>` options, where `format` is `human`, `json`, or `sarif`, and `destination` is `stdout`, `stderr`, or a caller-provided file path. Existing single-format `--format <format>` usage SHALL remain supported as one sink to its legacy standard stream; combining `--format` with `--output` SHALL be rejected as ambiguous.

All sinks SHALL consume one normalized result. Every file sink SHALL be rendered to a bounded temporary file in its destination directory and validated before the first destination is changed. Each destination SHALL then be replaced atomically where the host filesystem supports atomic same-directory replacement. Multi-file all-or-none commit SHALL NOT be claimed across independent paths or filesystems. If a replacement fails after another destination has already committed, the command SHALL report typed `partial-output` evidence identifying committed and uncommitted destinations, exit `2`, and SHALL NOT rerun validation. Input policy, baseline, snapshot, schema, and receipt paths SHALL not be overwritten. Duplicate/conflicting standard-stream destinations SHALL be rejected unless their ordering is explicitly deterministic and documented.

#### Scenario: Human plus JSON plus SARIF
- **WHEN** one invocation requests `human=stderr`, `json=report.json`, and `sarif=report.sarif`
- **THEN** policy loading, project evaluation, assembly scanning, baseline comparison, and contract execution occur once

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
