## ADDED Requirements

### Requirement: Health report evidence carries verifiable execution and authority context
The additive Health reporting-evidence envelope SHALL carry a versioned,
non-empty execution identifier and condition-set scope from the completed
Health request, plus canonical mode identity for every validation receipt.  It
SHALL emit the closed availability map for every receipt, using only the
documented authority keys and allowed wire values.  A declared available
authority SHALL include its canonical payload; an absent payload SHALL be
declared unavailable or not configured as appropriate.  Health SHALL not emit
an availability map that could cause a downstream consumer to treat missing
payload as complete.

#### Scenario: Health persists the requested context
- **WHEN** Health emits JSON for an evaluation supplied with an execution
  identifier and condition-set scope
- **THEN** the reporting-evidence envelope retains that identifier and scope
  and each validation receipt retains its canonical mode

#### Scenario: Optional evidence is explicitly not configured
- **WHEN** the completed Health evaluation has no configured topology or
  external-evidence authority
- **THEN** the availability map records the appropriate `not_configured`
  state and omits the corresponding payload
