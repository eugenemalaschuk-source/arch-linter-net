## ADDED Requirements

### Requirement: Self-policy governs discovered CLI command modules

The repository self-policy SHALL govern every direct child of `ArchLinterNet.Cli.Commands` through the module-container contract. During migration it SHALL run alongside the existing hand-authored command-independence rule and include a regression proving equivalent coverage of the discovered command inventory before the hand-authored peer list is retired.

#### Scenario: A new command cannot bypass sibling isolation
- **WHEN** a new direct CLI command module references an existing direct command module
- **THEN** `make lint-architecture` fails without requiring a contributor to add the new module to a manually maintained sibling list first

### Requirement: Self-policy enforces recursive convention purity and leaf direction

After audited migration debt is resolved, the repository self-policy SHALL strictly require that every production `Abstractions` folder contains only interfaces or abstract classes, every production `Exceptions` folder contains only exception-role types, and every production `Models` or `Exceptions` type has no first-party dependency. These rules SHALL apply recursively to CLI command modules and future domain containers.

#### Scenario: A helper type is hidden in Exceptions
- **WHEN** a non-exception helper record, interface, enum, struct, delegate, or class is added to a production `Exceptions` folder
- **THEN** `make lint-architecture` fails with the folder-purity contract identity

#### Scenario: A nested command abstraction imports command behavior
- **WHEN** an abstraction inside a command module references its module's application type
- **THEN** `make lint-architecture` fails with the module-profile direction contract identity

### Requirement: Self-policy rejects new god nodes and generic shared buckets

The repository self-policy SHALL reject undeclared production types in the `Cli.Commands` container root and generic `Common`, `Shared`, or `Utils` module buckets. A reusable capability SHALL be introduced as a separately named and owner-reviewed boundary outside the command container; the policy exception SHALL state its owner and reason.

#### Scenario: A formatter is added to the command container root
- **WHEN** a concrete formatter is added directly under `ArchLinterNet.Cli.Commands`
- **THEN** the strict architecture gate fails and identifies the container-root policy

#### Scenario: A reviewed integration boundary is used
- **WHEN** two command modules require the same stable output-formatting contract
- **THEN** they reference a named, separately governed integration boundary
- **AND** no direct command-to-command dependency is introduced

### Requirement: Partial-type governance remains complementary

The module policy SHALL complement, not weaken or replace, the strict production declaration-count rule delivered by `decompose-god-classes`. A module satisfying its folder shape with a handwritten partial aggregate still SHALL fail the production partial-type rule unless it is an explicitly reviewed generated-code exception.

#### Scenario: A module hides a handler in partial fragments
- **WHEN** a production command handler is split across multiple handwritten partial declarations
- **THEN** the architecture gate reports the declaration-count violation even if each fragment is in an otherwise valid module segment
