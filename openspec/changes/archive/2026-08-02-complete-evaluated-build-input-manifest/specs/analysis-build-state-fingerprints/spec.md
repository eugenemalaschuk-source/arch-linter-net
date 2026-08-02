## ADDED Requirements

### Requirement: Evaluated build-input manifest has an explicit cache-authorization outcome
For every selected project/output context, the system SHALL derive a bounded, deterministic `analysis-build-state/v1` evaluated build-input manifest and one explicit outcome: `verified-cache-eligible` or `cache-ineligible`. The manifest SHALL cover or explicitly reject project/import inputs, repository and linked compile inputs, generated/analyzer/additional/config inputs, compiler options, package/project/framework/assembly reference identities, SDK/global-property identities, and configuration/TFM/platform/RID. Unknown or unsupported input SHALL never silently produce an eligible result.

#### Scenario: Linked source changes
- **WHEN** a trusted repository-contained linked compile file changes
- **THEN** the manifest digest changes and any prior eligible receipt is not authorized for reuse

#### Scenario: Unsupported input is encountered
- **WHEN** the collector cannot prove an input's identity, containment, or effect on compilation
- **THEN** it returns `cache-ineligible` with a stable reason and a cache consumer must recompute rather than reuse facts

#### Scenario: Contexts differ
- **WHEN** configuration, target framework, platform, or runtime identifier differs
- **THEN** the corresponding manifest and cache-authorization context do not collide

### Requirement: Cache manifest identity is portable and trust bounded
The system SHALL represent trusted paths with canonical repository-relative logical coordinates, use ordinal ordering and SHA-256 content digests, and keep absolute roots, timestamps, and file sizes as non-authoritative evidence. It SHALL reject repository escape, ambiguous aliases, executable/argument selection from receipt/cache content, and TOCTOU changes between collection, artifact verification, and publication.

#### Scenario: Equivalent checkout
- **WHEN** trusted equivalent project contents are collected under distinct absolute checkout roots
- **THEN** their eligible manifest identities are equivalent

#### Scenario: Input changes during verification
- **WHEN** a fingerprinted input or required output changes after collection and before candidate publication
- **THEN** the result is not cache-authorized and no reusable candidate is published

### Requirement: Policy identity remains independent from cache-authorization build state
The system SHALL keep effective policy and requested analysis/session semantics separate from the evaluated build-input manifest. A policy-only change SHALL alter analysis/session identity without treating otherwise verified compiled artifacts as stale or changing the cache-eligibility classification.

#### Scenario: Policy-only edit
- **WHEN** only an effective policy input changes
- **THEN** the session identity changes while the manifest's build/artifact verification result remains unchanged
