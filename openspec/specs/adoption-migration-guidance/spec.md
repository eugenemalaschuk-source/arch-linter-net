# adoption-migration-guidance Specification

## Purpose

Define canonical evergreen adoption and migration guidance, thin status-correct
reference entrypoints, and documentation checks that keep compatibility behavior
discoverable without making a product package release part of the documentation
identity.

## Requirements

### Requirement: Adoption and upgrade guidance is canonical and release-neutral

The documentation SHALL publish one searchable evergreen guide that distinguishes
greenfield adoption from upgrading an existing ArchLinterNet policy. The guide
SHALL NOT use product package SemVer as its filename, route, heading identity, or
canonical navigation label. It SHALL show repository-owned package pinning via a
local tool manifest, a minimal root policy, assembly-free policy checking,
preparation/build, and strict validation before optional baseline, API snapshot,
report, cache, profile, or concurrency features.

The upgrade path SHALL direct users through reviewable imported-policy,
selector/source-set, planned-empty, baseline, finding, API-snapshot, build-state,
cache/profile, concurrency, cancellation, and packaged-schema transitions without
silently approving debt or inferring missing canonical identity.

#### Scenario: New adopter uses a minimal policy
- **WHEN** a new adopter follows the greenfield section
- **THEN** the documented commands succeed without requiring source sets,
  baselines, cache, profiling, or parallel execution configuration

#### Scenario: Existing adopter upgrades deliberately
- **WHEN** an adopter updates its repository-owned ArchLinterNet package pin
- **THEN** the guide directs it to inspect the installed tool/schema boundary,
  run policy checks and strict validation, and review changed compatibility
  evidence without creating a version-named successor guide

#### Scenario: Existing adopter requalifies a baseline
- **WHEN** an adopter encounters `changed`, `stale`, or `ambiguous` baseline
  lifecycle output after an identity correction
- **THEN** the guide directs the adopter to review, explicitly update or
  recapture, and then prune the baseline without automatically suppressing the
  finding

### Requirement: Reference entrypoints are thin and status-correct

The documentation SHALL provide copy-pasteable synthetic direct CLI, POSIX
shell, PowerShell, Make, Taskfile, Tilt, GitHub Actions, and generic CI
reference entrypoints. Repository/CI examples SHALL restore a reviewed local
tool manifest rather than embedding the current ArchLinterNet package version in
evergreen prose. Each wrapper SHALL pass structured arguments without
string-evaluated command construction, invoke the product once per requested
validation session, and preserve standard output and standard error routing.

Direct CLI, POSIX, PowerShell, Task invoked with `--exit-code`, and generic-CI
examples SHALL propagate the exact product exit code. The Make example SHALL
state that GNU Make cannot preserve a failed recipe's exact exit code and SHALL
write that code to a machine-readable artifact for the outer shell or CI caller
to propagate. PowerShell examples SHALL use native command invocation and
`$LASTEXITCODE` explicitly.

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

The guide and linked references SHALL document installed `schema list` and
`schema print` commands as the release source of truth for policy root,
fragment, baseline, API snapshot, build state, normalized finding, cache, and
profile formats. Product package SemVer SHALL NOT be mechanically transformed
into a schema URL.

Exact machine/document contract versions MAY appear where the version is itself
the compatibility contract. This includes persisted schema IDs, normalized
artifact versions, standards, target frameworks, and reproducible dependency
pins. Such versions SHALL remain distinct from product-release documentation
identity.

The guidance SHALL state repeatable `--report` syntax, separate ownership of
command `--output` artifacts, atomic/partial-output semantics, complete non-TTY
human output, typed JSON/SARIF/Testing equivalence, and stable numeric exit
categories. It SHALL include cache-disabled, opt-in-cache, sequential,
bounded-parallel, and cancellation-safe execution guidance without requiring
network access in a prepared environment.

#### Scenario: Offline CI needs a schema
- **WHEN** a prepared non-interactive CI job has an installed ArchLinterNet tool
  but no repository checkout or network access
- **THEN** the guide directs it to discover and print the exact packaged schema
  bytes with the installed CLI

#### Scenario: A report sink fails after another commits
- **WHEN** a user routes multiple validation reports and one later replacement
  fails
- **THEN** the guide describes typed `partial-output`, exit code 2, and the
  committed/uncommitted destination evidence without claiming a cross-file
  transaction

### Requirement: Evergreen documentation identity is enforced

README, MkDocs navigation, normal public documentation filenames/routes, and
current user guidance SHALL use durable conceptual identities rather than the
ArchLinterNet product package SemVer. Release-specific historical information
SHALL live in GitHub Releases/tags/issues/milestones or other explicit release
records rather than accumulating version-named evergreen Markdown pages.

Repository validation SHALL deterministically reject reintroduction of
version-named public docs paths/navigation and hard-coded current
ArchLinterNet-package pins in evergreen install/adoption guidance, while
allowing genuine machine/protocol/document versions and release-process SemVer
examples.

Historical archived OpenSpec change evidence MAY retain factual release
references and SHALL NOT become current documentation authority merely because
it remains in the repository.

#### Scenario: A maintainer adds a version-named migration page
- **WHEN** a public docs path or MkDocs navigation entry makes a product package
  SemVer part of an evergreen page identity
- **THEN** `lint-docs` fails with an actionable evergreen-documentation error

#### Scenario: A public page is updated
- **WHEN** a maintainer changes an affected public reference page
- **THEN** navigation and structural validation retain a reachable canonical
  version-neutral adoption/upgrade path
