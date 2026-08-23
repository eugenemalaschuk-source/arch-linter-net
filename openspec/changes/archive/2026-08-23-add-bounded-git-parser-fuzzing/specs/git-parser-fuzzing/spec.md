## ADDED Requirements

### Requirement: Synthetic Git binary-parser fuzz seam
The repository SHALL provide an executable fuzz harness whose synthetic,
byte-array input selects only loose-object header/payload validation, version-2
pack-index layout and offset lookup, pack-entry header decoding,
`OBJ_OFS_DELTA` reconstruction, or `OBJ_REF_DELTA` reconstruction. The harness
SHALL not locate, open, mutate, or otherwise consume a live Git repository.

The pack-index, pack-entry, `OBJ_OFS_DELTA`, and `OBJ_REF_DELTA` routes SHALL
each execute in both supported Git object-ID modes: 20-byte SHA-1 and 32-byte
SHA-256. The oracle SHALL accept only a canonical bounded parser result or the
existing fail-closed history diagnostic route; an unhandled exception, hang,
resource-limit breach, or partial success SHALL be a fuzzing failure.

#### Scenario: Valid synthetic parser input
- **WHEN** a versioned valid seed is replayed through its selected parser seam
- **THEN** the harness completes with a canonical bounded result and does not
  access a repository, network, or external input

#### Scenario: Malformed synthetic parser input
- **WHEN** a truncated or structurally invalid versioned seed is replayed
- **THEN** the harness completes through the fail-closed diagnostic route and
  does not surface a raw runtime exception

#### Scenario: Digest-sensitive routes
- **WHEN** a pack-index, pack-entry, OFS-delta, or REF-delta seed is replayed
- **THEN** the harness executes it once with a 20-byte digest and once with a
  32-byte digest before reporting the result

### Requirement: Bounded replay and synthetic corpus
The harness SHALL reject an input larger than 1 MiB before selected-parser
execution. Every replay and campaign invocation SHALL enforce a 100 ms
per-case limit and a 512 MiB process-memory limit. The user-facing `--replay`
command SHALL launch an isolated worker under those limits; the worker SHALL
not be directly reachable through the documented replay command.

The repository SHALL version a public-safe synthetic seed corpus and provide
deterministic materialization, single-input replay, and minimization commands.
Committed seeds SHALL contain only authored format fragments or test-derived
synthetic data and SHALL not contain private repository, adopter, path, secret,
or credential data. The AFL++ campaign SHALL set `AFL_HANG_TMOUT=100` and run
the container as the host runner UID/GID so its 0700/0600 findings remain
  readable to the host cleanup step. When candidates exist, the workflow SHALL
  encrypt the crash/hang files with the repository `GIT_PARSER_FUZZ_TRIAGE_KEY`
  secret and retain only the encrypted bundle and integrity sidecar for 14 days;
  raw inputs SHALL never be uploaded in plaintext.

#### Scenario: Oversized replay input
- **WHEN** a replay input is larger than 1 MiB
- **THEN** the harness rejects it before it dispatches to a selected parser
  seam

#### Scenario: Deterministic corpus replay
- **WHEN** the documented materialization and replay command is run for every
  committed seed and one malformed input
- **THEN** every case has the recorded time and memory limits and produces the
  same canonical or fail-closed outcome

#### Scenario: Bounded replay launcher

- **WHEN** a maintainer invokes the documented `--replay <input-file>` command
- **THEN** it starts a worker process with a hard 512 MiB allocation envelope
  (Windows Job Object, or the pinned .NET runtime Docker image with
  `--memory=512m --memory-swap=512m` on Linux/macOS), sets
  the managed heap guard to hex `0x20000000`, warms only the built-in
  public-safe corpus, and starts a 100 ms post-warm-up watchdog before the
  worker reads the candidate input

### Requirement: Finding promotion and corpus triage
The repository SHALL retain campaign crash and hang artifacts only long enough
for triage in an encrypted 14-day artifact and SHALL require review for safe
publication before a minimized input enters the committed corpus. Every
confirmed parser defect discovered by the campaign SHALL be minimized, replayed
under the recorded limits, and promoted to a deterministic NUnit regression
before the defect is closed.

#### Scenario: Campaign reports a candidate failure
- **WHEN** AFL++ reports a crash, timeout, resource-limit breach, or unexpected
  success
- **THEN** maintainers replay and minimize it, review it for public safety, and
  add a deterministic NUnit regression for each confirmed defect before closing
  the finding
