# architecture-health-badge-promotion Specification

## Purpose
Provides one versioned, reusable trusted-promotion contract for private and public Architecture Health consumers, preserving exact producer provenance while allowing transport-specific delivery through explicitly approved adapters.

## Requirements

### Requirement: Promotion uses approved configuration and exact producer provenance
The reusable promotion capability SHALL obtain repository, workflow, job, check, artifact, branch, event, and adapter selection from an approved configuration or registry that is independent of consumer-controlled pull-request artifacts. It SHALL prove the exact merged commit, base/head/tree relation, required check identity, workflow run and attempt, producer job, artifact identity, size, schema, digest, and canonical bytes before a ready publication. Names, conclusions, arbitrary URLs, or a generic workflow success SHALL not establish those facts.

#### Scenario: Mismatched producer evidence fails closed
- **WHEN** a producer run, check, artifact, tree, attempt, job, repository, or merge relation differs from approved configuration or the merged commit
- **THEN** promotion publishes the fixed unavailable state with a redacted actionable reason
- **AND** it does not reuse a different green artifact or preserve the previous ready state

#### Scenario: Consumer input cannot choose a producer
- **WHEN** a consumer-controlled input supplies a workflow name, artifact name, run URL, repository URL, or source checkout path that is not the approved configuration
- **THEN** promotion rejects the input before any publication write
- **AND** no consumer source, script, action, or generated artifact is executed

### Requirement: Promotion validates untrusted artifact bytes safely
The privileged promotion path SHALL treat downloaded artifacts and manifests as untrusted data. It SHALL enforce bounded archive and file sizes, safe relative paths, regular-file and UTF-8 constraints, duplicate/path-traversal/symlink rejection, exact manifest binding, and closed canonical disclosure validation before publishing bytes. It SHALL not repair, regenerate, reserialize, or calculate Architecture Health facts from rejected input.

#### Scenario: Hostile artifact is rejected without extraction side effects
- **WHEN** an artifact contains a traversal path, symlink, duplicate entry, oversized member, malformed UTF-8, duplicate JSON key, or unexpected file
- **THEN** validation rejects it with a fixed diagnostic
- **AND** no untrusted hook, extracted executable, or partial publication is created

#### Scenario: Canonical bytes remain unchanged
- **WHEN** a producer supplies a valid canonical disclosure payload and matching digest
- **THEN** the published bytes are byte-identical to the validated input
- **AND** the promotion layer does not recalculate Gate, Health, counts, color, or the semantic validity horizon

### Requirement: Adapters are explicit and bounded
The capability SHALL provide only the approved `github-raw`, `relay`, and `none` adapters. Adapter selection SHALL be typed and configuration-bound; arbitrary destination URLs, callback URLs, provider catalogues, private-repository raw publication, and unsupported authenticated/custom transports SHALL be rejected. The legacy public raw endpoint and its semantic bytes SHALL remain compatible.

#### Scenario: Private raw publication is rejected
- **WHEN** a private repository selects the raw adapter or a raw configuration targets a private/public-disclosure Relay mode
- **THEN** setup or promotion returns a migration or unsupported diagnostic before writing the destination
- **AND** no private identity or receipt is exposed through the raw endpoint

#### Scenario: None adapter records no public disclosure
- **WHEN** the approved `none` adapter is selected
- **THEN** promotion records only the private outcome needed by the caller
- **AND** it emits no public payload, URL, source identity, token, or private provenance

### Requirement: Publication and renewal are monotonic and metadata-only
Push/squash publication, bounded retry/recovery, and scheduled renewal SHALL use conditional writes bound to the current target context, generation, revocation epoch, idempotency key, and fixed deadline. Renewal SHALL revalidate current authorization, producer identity, required gate, artifact availability, and the product-owned semantic validity horizon without rerunning Architecture Health analysis on main. Older, cancelled, revoked, expired, or out-of-order operations SHALL not overwrite a newer or unavailable state.

#### Scenario: Renewal cannot extend elapsed semantic evidence
- **WHEN** a same-tree renewal is attempted after the stored product-owned validity horizon or after artifact retention/revocation invalidates the evidence
- **THEN** the destination becomes or remains unavailable
- **AND** renewal does not manufacture a new ready payload or extend the old lease

#### Scenario: Stale writer loses a race
- **WHEN** an older publish or renewal retries after a newer generation, revocation, cancellation, or unavailable transition has committed
- **THEN** the conditional write is rejected
- **AND** the newer destination state remains unchanged

### Requirement: Privileged execution and disclosure are constrained
The released reusable workflow/action SHALL execute only reviewed pinned project code, use a fresh GitHub OIDC token with the configured audience and pinned workflow identity for Relay publication, and keep short-lived GitHub metadata-read credentials inside the source job. It SHALL not checkout, build, or execute consumer main/PR scripts, arbitrary generated artifacts, untrusted hooks, unrestricted inherited secrets, or pass GitHub tokens to a Relay/render URL. Verification failures, authorization failures, and transport outages SHALL have distinct fixed redacted diagnostics.

#### Scenario: Consumer code is never executed by the publisher
- **WHEN** a reusable publisher handles a consumer request containing PR-controlled files or scripts
- **THEN** it resolves and validates immutable remote evidence without executing those files
- **AND** privileged steps have only the minimum declared permissions and approved action pins

#### Scenario: Relay authentication uses a fresh scoped token
- **WHEN** a Relay adapter publishes a validated payload
- **THEN** it requests a fresh OIDC token for the approved audience and workflow identity
- **AND** no GitHub API token, raw JWT, private receipt, or confidential identifier appears in public payloads, URLs, or diagnostics

### Requirement: Reusable installation is versioned and consumer-shaped
The capability SHALL expose a versioned reusable workflow/action with a documented approved configuration, compatibility metadata, and pinned reference suitable for an external synthetic private/public consumer. The reference repository SHALL consume the same contract through a thin adapter, and unsupported plans or missing required-check/ruleset visibility SHALL produce actionable unavailable results rather than weaker verification.

#### Scenario: External consumer can use the pinned candidate
- **WHEN** a synthetic consumer installs the reviewed workflow/action at its approved immutable reference and supplies valid configuration
- **THEN** it can publish a validated public or Relay-backed result without copying repository-specific verifier code
- **AND** the candidate records the exact component and configuration identity used

#### Scenario: Unsupported GitHub capability fails closed
- **WHEN** branch protection or ruleset APIs cannot prove the configured required gate because of a missing, unsupported, or forbidden capability response
- **THEN** promotion reports an actionable unavailable result
- **AND** it does not bypass the gate or assume a compatible plan shape
