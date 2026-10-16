# release-scope-preparation Specification

## Purpose

Automate preparation of reviewed release-scope declarations from structured release-story data, so the release-preparation workflow can generate and validate candidate scope without manually transcribing it.

## Requirements

### Requirement: Release stories provide a strict machine-readable scope block

The release-scope preparation tool SHALL read exactly one fenced TOML `release-scope/v1` block from the release story issue in `eugenemalaschuk-source/arch-linter-net`, explicitly passing that repository to GitHub CLI rather than using its ambient default. The block SHALL contain `schema`, `release_target`, `declaration_id`, `comments`, `required_items`, `excluded_items`, and `delivered_items` using the current declaration field semantics. Each required item SHALL contain `issue`, `finding`, and `summary`; each excluded or delivered item SHALL contain `issue` and a non-empty `reason`. The generated declaration's `story` value SHALL come from the GitHub issue number. The tool SHALL reject a missing or duplicate block, unsupported schema, unknown fields, malformed values, duplicate issue identities, or an invalid story/target identity before writing output.

#### Scenario: A valid release story scope is accepted

- **WHEN** the story contains exactly one supported scope block with a valid exact target and disjoint valid item inventories
- **THEN** the tool accepts the block as the source for the versioned declaration

#### Scenario: Scope data is ambiguous or malformed

- **WHEN** the story has no scope block, multiple blocks, an unsupported schema, an invalid target, unknown fields, duplicate issue identities, or an identity mismatch
- **THEN** the tool reports the specific validation error and leaves the declaration directory unchanged

### Requirement: The generator emits the existing tracked publication declaration

The release-scope preparation tool SHALL deterministically render a valid `checkpoint-b-release-scope-declaration/v2` declaration from the validated story block. It SHALL derive the output path from the exact release target under `tools/release/scopes/`, SHALL NOT accept a caller-supplied output path, and SHALL preserve all reviewed scope identities and reasons without inference from milestones, labels, commits, or live issue state.

#### Scenario: A declaration is generated for a release story

- **WHEN** a valid release story targets a version with no existing declaration
- **THEN** the tool writes the canonical declaration to the exact target path under `tools/release/scopes/`
- **AND** the generated declaration is compatible with the existing candidate authorization consumer

#### Scenario: Repeating generation does not change reviewed bytes

- **WHEN** generation for a target already exists and the newly rendered bytes are identical
- **THEN** the tool succeeds without changing the file

#### Scenario: Generation conflicts with an existing declaration

- **WHEN** the target path already contains different declaration bytes
- **THEN** the tool fails and preserves the existing reviewed declaration unchanged

### Requirement: Generation has a read-only preview mode

The release-scope preparation tool SHALL provide a preview operation that validates the story block and renders the exact declaration without writing to the repository.

#### Scenario: A maintainer previews a valid declaration

- **WHEN** preview runs for a valid release story
- **THEN** it displays or writes to standard output the exact bytes the write operation would generate
- **AND** it leaves all tracked files unchanged

### Requirement: Release preparation consumes generated declaration output

The release-preparation workflow SHALL run the generator for the reviewed release story, include its declaration output in the existing preparation PR, and verify it through generic declaration and generator tests before reporting preparation complete. Adding a new release target SHALL NOT require manually transcribing the same target and scope into a second per-release test inventory.

#### Scenario: New release authority is prepared

- **WHEN** the release-preparation workflow prepares a new exact target from a valid release story
- **THEN** it includes the generated declaration in the preparation PR
- **AND** validation proves the declaration target, story identity, and scope inventories match the reviewed source

#### Scenario: Existing publication workflow consumes the generated declaration

- **WHEN** the existing release workflow processes a candidate after the preparation PR is merged
- **THEN** it selects the same unique tracked declaration by exact candidate version and binds its identity and digest to candidate evidence
- **AND** the existing dry-run and separate publication procedure remains unchanged
