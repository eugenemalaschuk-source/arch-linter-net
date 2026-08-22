## ADDED Requirements

### Requirement: Test adapter exposes the typed architecture-debt gate
The Testing adapter SHALL expose a gate operation using the builder's configured policy and explicit baseline, with optional explicit policy-context artifacts. It SHALL return the typed Core-equivalent gate result so assertions can inspect evaluation, persistent-debt lifecycle, weakening findings, and the final decision without parsing formatted output.

#### Scenario: Adapter observes separate gate causes
- **WHEN** a test executes the gate with a matched baseline finding and an error-severity policy-weakening finding
- **THEN** the returned result distinguishes matched debt from weakening and reports a failed overall gate decision
