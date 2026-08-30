## MODIFIED Requirements

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
