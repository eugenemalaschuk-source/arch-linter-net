# self-dogfood-reference-workflow Specification

## Purpose

Defines the public-safe, reproducible reference workflow for applying released
ArchLinterNet forensics and AI-first architecture drift-control capabilities to
a real repository without treating advisory evidence as automatic authority.
## Requirements
### Requirement: Reproducible released-tool self-dogfood evidence

The repository SHALL retain a public-safe evidence record and canonical JSON
artifact for a real ArchLinterNet release range. The record SHALL identify the
released tool version, source commit, authored and resolved range operands,
policy/configuration identity, canonical artifact path, canonical artifact
digest, and the canonical .NET enrichment status. The canonical artifact digest
SHALL match the retained artifact bytes through a documentation lint or CI
check. The recorded tool SHALL be installed at its exact version in a
caller-owned isolated tool directory and invoked from that directory, without
relying on a local-tool manifest in an analysed repository or worktree. The
recorded canonical forensics command SHALL use separate `--from` and `--to`
operands, SHALL NOT represent a Git revision expression as a supported operand,
and SHALL omit `--enrich-dotnet` so its enrichment status is `not_requested`.
Any requested .NET enrichment observation SHALL be recorded separately as
advisory, environment-dependent evidence and SHALL NOT define the canonical
artifact digest.

#### Scenario: Maintainer reproduces the recorded run

- **WHEN** a maintainer follows the evidence record with the named isolated
  tool executable, repository revision, policy, and authored operands
- **THEN** they can recreate the canonical Git-only report and compare its
  digest without relying on a local-machine path, local-tool manifest, or
  private adopter data

#### Scenario: Retained artifact changes

- **WHEN** the canonical artifact bytes are altered without updating the
  documented digest
- **THEN** the documentation lint or CI check fails before the reference can be
  accepted

### Requirement: Material evidence receives a maintainer classification
The evidence record SHALL classify material hotspot, co-change/cluster,
bottleneck, OCP, and enrichment observations as confirmed technical pressure,
intentional architecture, insufficiently actionable signal, product UX or
diagnostic gap, or an evidence-linked focused follow-up. A high score alone
SHALL NOT authorize refactoring.

#### Scenario: Reader evaluates a prominent finding
- **WHEN** a reader inspects a material finding in the evidence record
- **THEN** the record identifies the applicable classification and explains why
  it is or is not action-worthy

### Requirement: Real AI-first drift-control reference path
The public reference SHALL demonstrate effective-policy context export,
complete architecture-result comparison, policy-weakening comparison,
deterministic remediation guidance, and known-versus-new debt gating using real
public-safe repository revisions and the actual repository policy. It SHALL
state that policy weakening is independent of persistent finding debt and that
baseline or policy updates remain explicit review actions.

#### Scenario: Adopter follows the drift-control path
- **WHEN** an adopter follows the reference commands with their own policy and
  complete results
- **THEN** they can distinguish architecture delta, policy-risk evidence,
  remediation guidance, and new persistent debt without treating any one as an
  automatic code or policy mutation

### Requirement: Explicit self-policy applicability decisions
The evidence record SHALL list each reviewed self-policy candidate and assign
exactly one of `adopt`, `already-covered`, `not-applicable`, or `defer`, with a
short evidence-based rationale. It SHALL NOT enable a capability merely to make
the reference example appear more complete.

#### Scenario: Reviewer evaluates a candidate capability
- **WHEN** a reviewer examines a candidate self-policy adoption
- **THEN** the record exposes its explicit decision and rationale rather than
  inferring adoption from the presence of documentation

### Requirement: Evergreen external adaptation guidance
The public reference SHALL explain prerequisites, packed-tool installation,
exact commands, input/range/policy meanings, representative output, advisory
versus authoritative boundaries, and how external repositories adapt the
workflow without copying ArchLinterNet-specific policy.

#### Scenario: External team adapts the example
- **WHEN** an external team reads the guide without access to repository
  maintainer context
- **THEN** it can choose its own policy and explicit revision range while
  understanding which commands produce evidence and which validation remains
  authoritative
