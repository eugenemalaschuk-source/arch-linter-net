# architecture-metric-measurement Specification

## Purpose
Provide a deterministic, read-only report of declared architecture metrics so
users can inspect a complete measured scope before authoring any budget gate.

## Requirements

### Requirement: Policies declare only supported metric definitions
The policy SHALL support an optional top-level `metrics` collection. Each
definition SHALL have a unique stable `id`, one metric kind from the closed
`architecture-metric-semantics` catalog, and exactly the native target fields
required by that kind. Component, footprint, and topology-slice metrics SHALL
identify one declared topology node; footprint metrics SHALL additionally
select exactly one `project` or `assembly` unit; public-surface metrics SHALL
identify exactly one existing public API surface contract by its
case-insensitive contract ID. A public-surface metric target is
configuration-invalid when matching strict and audit public API surface
contracts share that ID, because a metric has no mode selector. Definitions
SHALL NOT accept thresholds, baselines, formulas, scripts, arbitrary selectors,
or unsupported target/kind combinations.

#### Scenario: A component metric targets a declared node
- **WHEN** a policy defines an outgoing-component metric for one declared
  topology node
- **THEN** the definition is accepted and has one stable metric identity

#### Scenario: An invalid definition is rejected as policy configuration
- **WHEN** a metric definition omits its native target, duplicates an ID, or
  combines a kind with an unsupported target or unit
- **THEN** policy validation rejects it through the ordinary typed
  configuration path rather than reporting an unassessable measurement

#### Scenario: Public-surface target IDs are case-insensitive
- **WHEN** a public-surface metric targets `mysurface` and the policy declares
  exactly one public API surface contract with ID `MySurface`
- **THEN** policy validation accepts the target and measurement resolves that
  declared contract

#### Scenario: A cross-mode public-surface target is rejected
- **WHEN** strict and audit public API surface contracts share an ID and a
  public-surface metric targets that ID
- **THEN** policy validation rejects the metric as an ambiguous target rather
  than selecting either contract by order

### Requirement: Measurements reuse the deterministic metric authorities
For every declared metric selected for measurement, the system SHALL reuse the
native counting universe, topology mapping, dependency, external-group,
project/assembly ownership, public-surface selection, applicability, and
contributor semantics from `architecture-metric-semantics`. An evaluable
measurement SHALL expose its exact cardinality, stable native subject/effective
scope, and contributor identities in ordinal canonical order. It SHALL not
re-scan source, derive a transitive graph, count a repeated occurrence twice,
or substitute a partial known subset for the required universe.

#### Scenario: Repeated references leave a measured value unchanged
- **WHEN** one logical direct dependency is observed through multiple members
  or metadata occurrences
- **THEN** the measurement includes one canonical contributor and its value
  increases by one rather than by the occurrence count

#### Scenario: An explicitly complete empty scope measures zero
- **WHEN** a metric has no contributors and its complete required scope is
  proven
- **THEN** it reports the exact value zero with an evaluable scope state

### Requirement: Incomplete scope is explicit and cannot publish a partial value
Each selected metric SHALL project its expected membership and produced
applicability record through the shared governance-applicability evidence
model. Missing, unexpectedly empty, stale, unmapped, ambiguous, or otherwise
incomplete required metric evidence SHALL produce a typed unassessable scope
state with canonical reasons and provenance. An unassessable measurement SHALL
not publish a numeric value or a partial contributor set as trustworthy
measurement data.

#### Scenario: An unmapped direct endpoint makes a component measurement unassessable
- **WHEN** a selected component has a required direct relation whose endpoint
  is neither exactly mapped nor explicitly reviewed out of scope
- **THEN** its report has an unassessable applicability state and no measured
  component count

#### Scenario: A healthy measurement is not an architecture finding
- **WHEN** all selected metrics are evaluable, including metrics with value
  zero
- **THEN** measurement completes without creating a violation, warning, or
  SARIF result

### Requirement: The measure command emits deterministic Human and JSON reports
The CLI SHALL provide `measure` as a read-only command accepting a policy path,
optional metric-ID selection, and `human` or `json` output. It SHALL report
all declared metrics when no selection is supplied and reject unknown selected
IDs as invalid arguments. Human output SHALL show each metric's identity,
kind, native subject/effective scope, state, exact value when evaluable, and
bounded contributor evidence. JSON output SHALL use a documented schema
version and contain the same data with stable ordinal ordering. For an
evaluable measurement, JSON SHALL include the total contributor count and an
explicit truncation indicator when the requested contributor bound omits
entries. For an unassessable measurement, JSON SHALL use `null` for `value`,
`contributor_count`, `contributors`, and `contributors_truncated`; it SHALL
not serialize an unknown contributor universe as an empty list or zero count.
The command SHALL permit an explicit all-contributors request.

#### Scenario: JSON output is repeatable and schema-versioned
- **WHEN** the same policy, selected metric IDs, and analysis inputs are
  measured twice
- **THEN** the JSON documents have the same schema version, ordering, values,
  scope evidence, and contributors

#### Scenario: A bounded report preserves evidence about omitted contributors
- **WHEN** an evaluable metric has more contributors than the selected display
  bound
- **THEN** the report emits the bounded ordinal prefix, the full contributor
  count, and a true truncation marker

#### Scenario: An unassessable report does not imply an empty contributor universe
- **WHEN** a metric is unassessable because a required fact or topology endpoint
  is incomplete
- **THEN** its JSON `value`, `contributor_count`, `contributors`, and
  `contributors_truncated` fields are `null` rather than a numeric zero, empty
  list, or false truncation claim

### Requirement: Measurement has read-only fail-closed completion semantics
The measure command SHALL not rewrite policy, baseline, architecture findings,
or report inputs. It SHALL return success for an otherwise valid report in
which every selected measurement is evaluable, and return the existing
untrusted-result exit category when any selected metric is unassessable while
still emitting its typed report. Existing validation and SARIF behavior SHALL
remain unchanged unless the measure command is explicitly requested.

#### Scenario: Incomplete measurement does not look like a successful assessment
- **WHEN** a valid policy contains a selected metric with incomplete required
  measurement scope
- **THEN** the command emits the typed unassessable report and returns the
  untrusted-result exit category instead of a clean success result

#### Scenario: A policy without metrics is unchanged until measurement is requested
- **WHEN** an existing policy contains no metric definitions and a user runs
  an existing validation command
- **THEN** its validation behavior and output remain unchanged
