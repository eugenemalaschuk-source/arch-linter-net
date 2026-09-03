## MODIFIED Requirements

### Requirement: README quality signal badge
The repository README SHALL display Main quality, dynamic Codecov coverage,
dynamic Architecture Health, and live SonarCloud badges as distinct signals.
The Architecture Health badge SHALL resolve through the repository's stable
public endpoint payload and SHALL describe canonical Health, explicit waiver
debt, and effective policy-control count. It SHALL not be sourced from a
generic GitHub workflow-status endpoint or represent a strict self-policy pass.

#### Scenario: Quality badges and explanation are present
- **WHEN** a reader views the README
- **THEN** it shows a Main quality badge sourced from `main-quality.yml`
- **AND** it keeps the dynamic Codecov coverage badge
- **AND** it shows an Architecture Health badge sourced from the stable public
  endpoint payload
- **AND** it shows live SonarCloud badges for the configured SonarCloud project
- **AND** it links to documentation explaining that Main quality, Architecture
  Health, architecture coverage, and SonarCloud quality are distinct signals

### Requirement: Architecture PR report producer runs in the existing CI workflow

The architecture report producer SHALL run strict/audit coverage, canonical Health/change
artifacts, the CLI-rendered PR report artifact, and the CLI-rendered Architecture Health badge
payload inside a dedicated read-only `ci.yml` job that is independently schedulable from
repository lint, coverage/Sonar, and the other pull-request validation jobs. Because this job
does not share a runner or checkout with a job that builds the CLI/Testing projects, it SHALL
build those projects itself before invoking the CLI in `--no-build` mode. It SHALL not have
pull-request write permission or a comment-writing step.

The producer SHALL bind its badge payload in a bounded immutable manifest containing the
repository, pull-request number, target base ref and SHA, PR head SHA and Git-tree identity,
producer run ID and attempt, fixed payload path, byte count, and SHA-256. It SHALL upload only
the exact CLI-generated payload and its manifest as the named badge-promotion artifact. Workflow
glue SHALL not derive Health, ignore debt, rule count, colors, or badge message text.

#### Scenario: Producer builds and renders independently

- **WHEN** the architecture report producer job runs
- **THEN** it builds `ArchLinterNet.Cli` and `ArchLinterNet.Testing` before it runs the coverage,
  Health, change, report, and badge CLI steps
- **AND** it does not depend on a build performed by repository lint or another job

#### Scenario: Badge evidence is inert and bound to the validated PR tree

- **WHEN** the producer obtains a canonical Health document and generates its badge payload
- **THEN** it uploads the exact payload with a manifest bound to that PR, base context, run, head
  SHA, and head Git-tree identity
- **AND** it does not publish the payload or execute it as workflow code

## ADDED Requirements

### Requirement: Architecture Health badge promotion verifies merged-tree identity
Trusted automation triggered by a `push` to `main` SHALL publish the Architecture Health payload
only after it resolves exactly one merged pull request for that commit and verifies the repository,
target base context, merged commit, successful required `Architecture Coverage` PR producer run,
non-expired named artifact, manifest binding, and byte hash. It SHALL compare the immutable Git
tree identity of the validated PR head with the pushed merged `main` commit; matching commit SHA
alone SHALL not satisfy this requirement.

The publisher SHALL transport the complete validated CLI-generated payload unchanged to one fixed,
repository-controlled public endpoint and may write separate publication metadata. It SHALL not
check out or execute PR-controlled artifact content, recreate badge semantics in workflow code,
rerun architecture analysis, modify policy/baseline state, or deploy GitHub Pages/MkDocs. If any
proof or artifact is missing, stale, failed, expired, malformed, oversized, ambiguous, or
mismatched, it SHALL fail closed by replacing the stable endpoint with the CLI-generated explicit
unassessable payload and metadata rather than leaving an older healthy payload represented as
current.

#### Scenario: Squash merge promotes an exact-tree payload
- **WHEN** a required successful Architecture Coverage PR run produced a valid manifest-bound
  badge payload and the squash-merged `main` commit has the same Git tree as that PR head
- **THEN** the publisher transports that exact payload to the stable endpoint
- **AND** it records separate metadata binding the publication to the merged commit and validated
  producer evidence

#### Scenario: Same-looking metadata with another tree is rejected
- **WHEN** the manifest and pull-request metadata appear valid but the validated PR-head tree and
  pushed `main` tree differ
- **THEN** the publisher does not publish the ready payload
- **AND** the stable endpoint becomes the explicit unassessable payload

#### Scenario: Stale, failed, or unavailable evidence fails closed
- **WHEN** the associated PR, required producer run, artifact, manifest, or payload is missing,
  stale, failed, expired, malformed, or inconsistent
- **THEN** the publisher does not reuse a prior healthy payload as the current badge
- **AND** it publishes only the CLI-generated unassessable payload and bounded publication metadata

#### Scenario: Badge-only publication does not duplicate main validation or docs deployment
- **WHEN** a verified main badge publication runs
- **THEN** it does not execute the architecture validation matrix, `make acceptance`, or a
  GitHub Pages/MkDocs deployment
- **AND** it updates only the fixed static badge endpoint and optional publication metadata

## REMOVED Requirements

### Requirement: Dedicated architecture-policy workflow
**Reason**: Pull requests now provide the authoritative complete validation, and a generic
workflow-status badge is not a truthful Architecture Health signal.

**Migration**: Keep the legacy CLI `badge architecture-policy` projection for explicit consumers;
use the main publisher's verified Architecture Health endpoint for the README.
