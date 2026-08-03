## MODIFIED Requirements

### Requirement: Portable path normalization and containment
The system SHALL use repository-relative paths with `/` separators and ordinal comparison/order for stable identity, SHALL exclude absolute checkout paths and host-specific prefixes from stable identity, and SHALL reject traversal, symbolic-link, or junction paths before fingerprinting when any candidate path segment is a reparse point. External or ambiguous paths not covered by a typed logical coordinate SHALL fail as unverifiable.

#### Scenario: Windows and POSIX path equivalence
- **WHEN** the same repository path is discovered as `src\\Product\\Product.csproj` on Windows and `src/Product/Product.csproj` on POSIX
- **THEN** both are fingerprinted using `src/Product/Product.csproj`

#### Scenario: Selected path escapes through a symlink
- **WHEN** a selected source or project input resolves outside the repository root and no versioned external-input declaration exists
- **THEN** preflight reports an unverifiable input and no contract executes

#### Scenario: Symlink is an ancestor directory
- **WHEN** an explicit source, import, or project reference appears below a symbolic-link or junction directory
- **THEN** the collector does not read or hash the candidate as authoritative evidence and returns `cache-ineligible`

#### Scenario: Case aliases collide
- **WHEN** two discovered repository-relative spellings map to the same host file on a case-insensitive file system
- **THEN** preflight rejects the ambiguous identity instead of silently collapsing the entries

### Requirement: Incomplete static evidence is never cache authorization
The system SHALL classify SDK-style evaluation, uninspected imports, unresolved references/analyzers, missing companion-artifact evidence, symlink/reparse-point inputs, and exhausted collection budgets as `cache-ineligible`. It SHALL apply count and aggregate-byte budgets before reading further candidate input bytes and SHALL stop recursive collection before traversing additional candidate files after either budget is exhausted.

#### Scenario: SDK project lacks evaluated evidence
- **WHEN** a selected SDK-style project cannot prove SDK, implicit-import, global-property, analyzer, and framework identities
- **THEN** its outcome is `cache-ineligible` and it cannot authorize reuse

#### Scenario: Symlink escapes repository
- **WHEN** a candidate input is a symlink or has a symlink or reparse-point ancestor
- **THEN** the collector does not hash it as authoritative evidence and returns `cache-ineligible`

#### Scenario: Input budget is exhausted
- **WHEN** adding a candidate would exceed the manifest count or aggregate-byte budget
- **THEN** the collector returns `cache-ineligible` and stops recursive enumeration without probing subsequent candidate files

## ADDED Requirements

### Requirement: Matching fail-closed receipt remains consistent
The system SHALL compare a receipt's evaluated-manifest digest, cache-eligibility outcome, and normalized ineligibility reasons with the current manifest. A receipt and manifest that agree on `cache-ineligible` SHALL remain consistent evidence and SHALL NOT receive `receipt-manifest-mismatch` solely because the outcome is ineligible.

#### Scenario: Matching cache-ineligible receipt
- **WHEN** a receipt records the same manifest digest, `cache-ineligible` outcome, and rejection reasons as the current manifest
- **THEN** preflight retains the real rejection reasons without adding `receipt-manifest-mismatch`

#### Scenario: Receipt cache outcome differs
- **WHEN** a receipt's digest, eligibility outcome, or normalized rejection reasons differ from the current manifest
- **THEN** preflight adds `receipt-manifest-mismatch` and does not authorize cache reuse
