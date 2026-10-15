## ADDED Requirements

### Requirement: Topology readers can reuse prepared receipts without building
Topology capture, diff, and verify SHALL accept `--use-prepared-receipts` and pass the selection through the existing Core preflight and isolated target-loading path. After one authoritative preparation, readers SHALL analyze the same selected outputs without build, restore, or output mutation. Missing or stale receipts SHALL remain fail-closed. Captured subjects and relationships SHALL be complete; topology operations SHALL NOT report success from a partial reflected type universe.

#### Scenario: Capture, diff, and verify reuse unchanged prepared artifacts
- **WHEN** capture, diff, or verify runs in an independent CLI process with `--use-prepared-receipts` after one successful `--ensure-built` preparation
- **THEN** it reports current preflight and preserves the canonical observation, topology findings, and strict/audit outcomes of the prepared reference run
- **AND** no reader builds, restores, or rewrites a selected project or dependency output

#### Scenario: Type capture preserves every consumer subject
- **WHEN** a type capture analyzes the prepared consumer fixture whose public `Scope` type implements `IServiceScope`
- **THEN** the successful capture includes the complete expected subject set, including `Repro.Consumer.Scope`
- **AND** a partial type set cannot satisfy the successful-capture result

#### Scenario: Incomplete type universe fails closed
- **WHEN** any topology operation cannot load the complete type universe needed for its observation
- **THEN** the operation returns an analysis error rather than a successful partial topology result
