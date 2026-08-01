## ADDED Requirements

### Requirement: Cancellation completion is complete and non-retroactive
The system SHALL retain every configured file sink in cancellation evidence, SHALL not reclassify fully delivered streams after publication, and SHALL drain asynchronous child output before reading diagnostics.

#### Scenario: Cancellation before rendering
- **WHEN** cancellation is observed before report rendering with file sinks configured
- **THEN** every configured file destination is reported as uncommitted

#### Scenario: Child process exits during polling
- **WHEN** a child process exits while async stdout or stderr callbacks remain pending
- **THEN** diagnostic output is read only after parameterless `WaitForExit()` completes

### Requirement: Shared application seams observe cancellation
Baseline, public-API, policy composition, hashing, receipt publication, and final outcome construction SHALL observe the caller token before publishing a completed result.

#### Scenario: Cancellation during shared pipeline work
- **WHEN** the caller cancels during one of the shared pipeline phases
- **THEN** the operation raises cancellation and publishes no completed result or receipt
