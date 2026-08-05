# adoption-migration-guidance Specification

## Purpose

Define the canonical, release-qualified 0.5.1 adoption and migration guidance,
thin status-correct reference entrypoints, and documentation checks that keep
the public compatibility contract discoverable and safe.
## Requirements
### Requirement: A 0.5.1 adoption and migration guide is canonical and safe
The documentation SHALL publish one searchable 0.5.1 guide that distinguishes
greenfield adoption from upgrading a supported 0.5.0 policy. It SHALL identify
0.5.1 as the sole public stabilization release and Checkpoint A as internal,
non-release evidence. The greenfield path SHALL show pinned installation, a
minimal root policy, assembly-free policy checking, preparation/build, and
strict validation before optional baseline, API snapshot, report, cache,
profile, or concurrency features. The upgrade path SHALL direct users through
reviewable imported-policy, selector/source-set, planned-empty, baseline,
finding, API-snapshot, build-state, cache/profile, concurrency, cancellation,
and packaged-schema transitions without silently approving debt or inferring
missing canonical identity.

#### Scenario: New adopter uses a minimal policy
- **WHEN** a new adopter follows the greenfield section
- **THEN** the documented commands succeed without requiring source sets,
  baselines, cache, profiling, or parallel execution configuration

#### Scenario: Existing adopter requalifies a baseline
- **WHEN** a 0.5.0 adopter encounters `changed`, `stale`, or `ambiguous`
  baseline lifecycle output after an identity correction
- **THEN** the guide directs the adopter to review, explicitly update or
  recapture, and then prune the baseline without automatically suppressing the
  finding

### Requirement: Reference entrypoints are thin and status-correct
The documentation SHALL provide copy-pasteable synthetic direct CLI, POSIX
shell, PowerShell, Make, Taskfile, Tilt, GitHub Actions, and generic CI
reference entrypoints. Each wrapper example SHALL locate or install a pinned
release, pass structured arguments without string-evaluated command
construction, invoke the product once per requested validation session,
preserve standard output and standard error routing. Direct CLI, POSIX,
PowerShell, Task invoked with `--exit-code`, and generic-CI examples SHALL
propagate the exact product exit code. The Make example SHALL state that GNU
Make cannot preserve a failed recipe's exact exit code and SHALL write that code
to a machine-readable artifact for the outer shell or CI caller to propagate.
PowerShell examples SHALL use native command invocation and `$LASTEXITCODE`
explicitly.

#### Scenario: A POSIX consumer supplies an argument with whitespace
- **WHEN** a consumer adapts the documented POSIX entrypoint with a policy path
  containing whitespace
- **THEN** the argument remains one argument and no shell evaluation is used

#### Scenario: A PowerShell validation fails
- **WHEN** the documented PowerShell entrypoint invokes a validation that exits
  with code 1 or 2
- **THEN** it returns that same value through `$LASTEXITCODE` without replacing
  the command's standard streams

#### Scenario: A Make validation fails
- **WHEN** the documented Make target runs a validation that exits with code 1
  or 2
- **THEN** its outer shell or CI caller reads and returns the exact saved status
  artifact instead of interpreting GNU Make's own failure status as the product
  status

### Requirement: Offline and non-interactive guidance uses installed contracts
The guide and linked references SHALL document the installed `schema list` and
`schema print` commands as the release source of truth for policy root,
fragment, baseline, API snapshot, build state, normalized finding, cache, and
profile formats. They SHALL state the repeatable `--report` syntax, the
separate ownership of command `--output` artifacts, atomic/partial-output
semantics, complete non-TTY human output, typed JSON/SARIF/Testing equivalence,
and stable numeric exit categories. They SHALL include cache-disabled,
opt-in-cache, sequential, bounded-parallel, and cancellation-safe execution
guidance without requiring network access in a prepared environment.

#### Scenario: Offline CI needs a schema
- **WHEN** a prepared non-interactive CI job has an installed 0.5.1 tool but no
  repository checkout or network access
- **THEN** the guide directs it to discover and print the exact packaged schema
  bytes with the installed CLI

#### Scenario: A report sink fails after another commits
- **WHEN** a user routes multiple validation reports and one later replacement
  fails
- **THEN** the guide describes typed `partial-output`, exit code 2, and the
  committed/uncommitted destination evidence without claiming a cross-file
  transaction

### Requirement: Public documentation is consistent and externally checkable
The README, installation, migration, troubleshooting, policy, CLI, Testing,
output, schema, capability, support, AI, and release-reference documentation
SHALL link to or use the canonical 0.5.1 terminology and contain only synthetic
adopter identities. Repository validation SHALL check that the canonical guide
and entrypoint reference are navigable and retain their required safe command
and release-boundary statements. The final packed-artifact gate SHALL remain
the authority that executes the copied commands against freshly packed local
artifacts.

#### Scenario: A public page is updated
- **WHEN** a maintainer changes an affected public reference page
- **THEN** navigation and structural validation retain a reachable canonical
  migration path and do not reintroduce a Checkpoint A release claim
