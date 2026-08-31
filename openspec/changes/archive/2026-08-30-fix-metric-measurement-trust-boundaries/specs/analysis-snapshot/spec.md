## ADDED Requirements

### Requirement: Measurement cancellation is terminal for a snapshot
The snapshot SHALL apply its cancellation lifecycle uniformly to `Measure()`
and `Evaluate()`. If cancellation is observed while a measurement lazily
materializes analysis facts, the snapshot SHALL become cancelled before the
operation rethrows, and all later measurement or validation attempts SHALL be
rejected as reuse of a cancelled snapshot.

#### Scenario: A cancelled measurement cannot be followed by evaluation
- **WHEN** `Measure()` observes cancellation while materializing its analysis
  session
- **THEN** a subsequent `Measure()` or `Evaluate()` on that snapshot throws
  cancellation rather than reusing the partial session
