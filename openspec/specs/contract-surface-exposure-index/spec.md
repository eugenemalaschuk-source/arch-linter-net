# contract-surface-exposure-index Specification

## Purpose
Provide deterministic reusable evidence of the type references a visible .NET contract exposes, including signature shapes and compiled custom-attribute metadata, for later architecture-governance contracts.

## Requirements

### Requirement: Index visible contract exposure evidence
The system SHALL make reusable exposure evidence available for a caller-selected visible type surface without defining policy selection, target allow/deny rules, semantic roles, or reviewed API membership. A normalized visible-surface shape SHALL control which declared members and nested types contribute evidence and SHALL be part of the session-cache identity; callers remain authoritative for selecting roots. Each exposure record SHALL identify the declaring visible type, the metadata or member site, a stable path, and the referenced type's assembly-qualified identity.

#### Scenario: Effective reviewed surface supplies the roots
- **WHEN** a caller supplies types selected through an existing reviewed public-API surface
- **THEN** the exposure evidence SHALL be computed for those supplied roots without re-evaluating their API membership or changing their semantic roles

#### Scenario: Same full type name is present in two assemblies
- **WHEN** two referenced types have the same full name but different assembly identities
- **THEN** the index SHALL retain them as distinct exposure targets and paths

#### Scenario: One root is requested with two visible-surface shapes
- **WHEN** the same caller-selected root is requested with exported and internal-visible shapes
- **THEN** the index SHALL materialize and cache distinct evidence sets without reselecting the root

### Requirement: Traverse visible signature shapes recursively
The system SHALL record deterministic exposure paths from visible types and visible members through constructor parameters, method parameters and returns, properties, fields, events, delegate invoke signatures, base types, implemented interfaces, generic arguments, generic constraints, arrays, nullable wrappers, tuples, nested generic containers, and participating nested types. A property or event SHALL participate when at least one of its accessors matches the requested visible-surface shape. It SHALL retain path segments that distinguish the declaring member or relationship from structural nesting.

#### Scenario: Nested generic return type exposes its nested target
- **WHEN** a visible member returns a nested generic shape such as `Task<Envelope<Customer>>`
- **THEN** the index SHALL record deterministic path evidence from that member through both generic containers to `Customer`

#### Scenario: Generic constraint exposes its bound
- **WHEN** a visible type or member generic parameter has a type constraint
- **THEN** the index SHALL record the constraint type as an exposure with a generic-constraint path segment

#### Scenario: Delegate signature exposes parameter and return types
- **WHEN** a visible delegate type participates in the selected surface
- **THEN** the index SHALL record its visible invoke parameter and return types with distinct stable path segments

### Requirement: Include visible compiled custom-attribute metadata
The system SHALL record a custom attribute type attached to a visible type, visible member, visible parameter, visible return value, or visible generic parameter as exposure evidence. It SHALL record type-valued and enum-typed custom-attribute arguments when compiled metadata provides a referenced type identity, using path segments that distinguish the attribute from its argument.

#### Scenario: Attribute type and typeof argument are exposed
- **WHEN** a visible contract site carries a custom attribute whose constructor or named argument contains `typeof(Customer)`
- **THEN** the index SHALL record separate deterministic exposure paths for the attribute type and for `Customer` as an attribute argument

#### Scenario: Primitive and string arguments are not fabricated as types
- **WHEN** a visible custom attribute argument is a primitive or a string
- **THEN** the index SHALL NOT interpret that value as a type reference

### Requirement: Preserve deterministic, bounded, complete evidence
The system SHALL produce a canonical order independent of reflection traversal order and SHALL terminate cyclic or self-referential signature structures deterministically. Different visible paths to the same target SHALL remain independently explainable. If reflection cannot obtain a required signature or metadata fact, the index SHALL preserve explicit incomplete evidence rather than silently presenting a shortened graph as complete.

#### Scenario: Cyclic signature shape terminates
- **WHEN** visible signature facts contain a cycle or self-reference
- **THEN** index construction SHALL terminate and retain deterministic non-cyclic path evidence

#### Scenario: Required signature fact cannot be resolved
- **WHEN** a visible first-party contract site has a signature or metadata fact that reflection cannot resolve
- **THEN** the index SHALL expose deterministic incomplete evidence identifying the affected site so a consuming governance contract can be assessed as unassessable rather than complete
