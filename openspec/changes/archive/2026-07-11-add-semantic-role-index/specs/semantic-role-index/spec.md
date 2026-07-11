## ADDED Requirements

### Requirement: Role index is scoped to one validation run
The system SHALL provide an `ArchitectureRoleIndex` constructed once per validation run from `ArchitectureAnalysisSession`, exposed alongside `TypeIndex`/`ReferenceGraph`, and never reused across assemblies or runs.

#### Scenario: Single index for a run
- **WHEN** a validation run executes multiple contract checks or classification queries
- **THEN** all of them read from the same `ArchitectureRoleIndex` instance, and no new index is constructed mid-run

#### Scenario: New run gets a new index
- **WHEN** two separate validation runs execute against the same or different target assemblies
- **THEN** each run constructs its own `ArchitectureRoleIndex`, and no cached descriptor from one run is visible to the other

### Requirement: Role descriptors are computed lazily in a single cached pass
The system SHALL compute every classified type's role descriptor (role, metadata, classification source, evidence) on first access to the index and reuse the cached result for all subsequent lookups within the same session, without re-invoking `ArchitectureAttributeRoleExtractor` per lookup.

#### Scenario: Repeated lookups reuse the cached pass
- **WHEN** two different callers within the same session each query the role index (e.g. a lookup API and a diagnostics read)
- **THEN** the underlying extraction pass over the session's type universe executes once for the session, not once per query

#### Scenario: Index is not accessed until first use
- **WHEN** a validation run does not query the role index at all
- **THEN** the extraction pass never executes for that run

### Requirement: Lookup API resolves a type's role descriptor
The system SHALL provide a lookup API that, given a `Type` from the session's type universe, returns whether the type has a resolved role and, if so, its role, metadata, classification source, and evidence.

#### Scenario: Classified type resolves a descriptor
- **WHEN** a type carries an attribute matching a `classification.attributes` or `classification.assembly_attributes` entry
- **THEN** the lookup API returns a descriptor with that type's resolved role, metadata, and classification source

#### Scenario: Unclassified type resolves no descriptor
- **WHEN** a type matches no `classification.attributes`/`classification.assembly_attributes` entry, directly or via its declaring assembly
- **THEN** the lookup API reports no role descriptor for that type

### Requirement: Lookup API enumerates all classified types
The system SHALL provide a lookup API that enumerates every type in the session's type universe that resolved a role, for use by selectors and future coverage checks.

#### Scenario: Enumeration includes every classified type
- **WHEN** the role index has computed descriptors for a run
- **THEN** the enumeration API returns exactly the set of types that have a resolved role descriptor, with no duplicates

### Requirement: Descriptors are explainable by classification source
Each role descriptor SHALL report which classification source produced the winning assignment (`type_attribute` or `assembly_attribute`), enabling diagnostics to explain why a type was assigned a role.

#### Scenario: Descriptor names the winning source
- **WHEN** a type's role comes from a type-level attribute overriding an assembly-level attribute
- **THEN** the descriptor's classification source is `type_attribute`, not `assembly_attribute`

### Requirement: Conflicts and metadata failures are exposed by the index
The system SHALL expose the run's classification conflicts and metadata-extraction failures as index-level collections, computed as part of the same single cached pass used for role descriptors.

#### Scenario: Index conflicts match extractor output
- **WHEN** the role index computes its cached pass for a run
- **THEN** the index's conflict and metadata-failure collections are identical in content to what `ArchitectureAttributeRoleExtractor.Extract` would report for every type in the session's type universe

### Requirement: Empty classification configuration short-circuits to an empty index
When `Document.Classification` declares no `attributes` or `assembly_attributes` entries, the role index SHALL produce no role descriptors, no conflicts, and no metadata failures, without invoking extraction per type beyond minimal initialization.

#### Scenario: Policy without classification section yields an empty index
- **WHEN** a policy document declares no `classification` section
- **THEN** the role index's descriptor enumeration, conflicts, and metadata failures are all empty, and existing namespace-only contract behavior is unaffected

### Requirement: JSON output exposes discovered role descriptors
The `validate` command's JSON and CI-artifact output SHALL include a `classification_roles` array describing every classified type's subject (type full name), role, metadata, and classification source, deterministically ordered by subject.

#### Scenario: JSON output includes classification_roles
- **WHEN** `validate --format json` runs against a policy whose classification produces at least one resolved role
- **THEN** the JSON payload includes a `classification_roles` array with one entry per classified type, sorted by subject

#### Scenario: No classification produces an empty array, not an omitted field
- **WHEN** `validate --format json` runs against a policy with no `classification` section
- **THEN** the JSON payload includes `classification_roles` as an empty array
