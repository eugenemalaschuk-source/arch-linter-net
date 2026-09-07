## MODIFIED Requirements

### Requirement: README quality signal badge
The repository README SHALL display Main quality, dynamic Codecov coverage, dynamic Architecture Health, and live SonarCloud badges as distinct signals. The Architecture Health badge SHALL resolve through the repository's stable public endpoint payload and SHALL describe the independent canonical Gate, canonical Health, explicit waiver debt, and effective policy-control count. It SHALL not be sourced from a generic GitHub workflow-status endpoint or represent a strict self-policy pass. Its navigation SHALL make the current public receipt and freshness diagnostics discoverable.

#### Scenario: Quality badges and explanation are present
- **WHEN** a reader views the README
- **THEN** it shows a Main quality badge sourced from `main-quality.yml`
- **AND** it keeps the dynamic Codecov coverage badge
- **AND** it shows an Architecture Health badge sourced from the stable public endpoint payload
- **AND** it shows live SonarCloud badges for the configured SonarCloud project
- **AND** it links to the current Architecture Health publication receipt and documentation that distinguishes raw-source freshness from Shields, README, and Camo rendering delay
- **AND** it links to documentation explaining that Main quality, Architecture Health, architecture coverage, and SonarCloud quality are distinct signals

### Requirement: Architecture Health badge promotion verifies merged-tree identity
Trusted automation triggered by a `push` to `main` SHALL publish the Architecture Health payload only after it resolves exactly one merged pull request for that commit and verifies the repository, target base context, merged commit, successful required `Architecture Coverage` PR producer run, non-expired named artifact, manifest binding, and byte hash. It SHALL compare the immutable Git tree identity of the validated PR head with the pushed merged `main` commit; matching commit SHA alone SHALL not satisfy this requirement.

The publisher SHALL transport the complete validated CLI-generated payload unchanged to one fixed, repository-controlled public endpoint and atomically write a versioned receipt. The receipt SHALL bind the repository; analyzed base, PR-head, and merged-main commit/tree identities; PR number; producer run and attempt; publisher run and attempt; payload SHA-256; status/reason; and publication time. It SHALL not check out or execute PR-controlled artifact content, recreate Gate or Health semantics in workflow code, rerun architecture analysis, modify policy/baseline state, or deploy GitHub Pages/MkDocs. If any proof or artifact is missing, stale, failed, expired, malformed, oversized, ambiguous, or mismatched, it SHALL fail closed by replacing the stable endpoint with the CLI-generated explicit unassessable payload and its receipt rather than leaving an older healthy payload represented as current.

#### Scenario: Squash merge promotes an exact-tree payload
- **WHEN** a required successful Architecture Coverage PR run produced a valid manifest-bound badge payload and the squash-merged `main` commit has the same Git tree as that PR head
- **THEN** the publisher transports that exact payload to the stable endpoint
- **AND** it atomically records a receipt binding the analyzed and merged identities, producer and publisher attempts, payload digest, and publication time

#### Scenario: Same-looking metadata with another tree is rejected
- **WHEN** the manifest and pull-request metadata appear valid but the validated PR-head tree and pushed `main` tree differ
- **THEN** the publisher does not publish the ready payload
- **AND** the stable endpoint becomes the explicit unassessable payload

#### Scenario: Stale, failed, or unavailable evidence fails closed
- **WHEN** the associated PR, required producer run, artifact, manifest, or payload is missing, stale, failed, expired, malformed, or inconsistent
- **THEN** the publisher does not reuse a prior healthy payload as the current badge
- **AND** it publishes only the CLI-generated unassessable payload and bounded publication receipt

#### Scenario: Badge-only publication does not duplicate main validation or docs deployment
- **WHEN** a verified main badge publication runs
- **THEN** it does not execute the architecture validation matrix, `make acceptance`, or a GitHub Pages/MkDocs deployment
- **AND** it updates only the fixed static badge endpoint and publication receipt
