## ADDED Requirements

### Requirement: Unmatched manual ignores feed canonical waiver lifecycle
The system SHALL retain unmatched-match evidence for every manual ignore and
use it to classify the corresponding architecture waiver as stale when no
higher-precedence invalid or expiry state applies. It SHALL preserve the
existing unmatched-ignore output for legacy consumers while exposing the
canonical lifecycle record as the authoritative waiver state.

#### Scenario: Matching and lifecycle records stay aligned
- **WHEN** a manual ignore stops matching all governed findings
- **THEN** existing unmatched-ignore evidence and the related waiver lifecycle
  record identify the same contract, matcher, and policy location
