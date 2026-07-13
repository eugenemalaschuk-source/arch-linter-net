## ADDED Requirements

### Requirement: AI-first semantic-role workflow guidance
The documentation SHALL explain a repeatable workflow for AI agents that starts from repository facts, classifies new code with reviewed semantic roles and metadata, proposes narrow schema-backed policy changes, and validates the result before review.

#### Scenario: Agent adds a new feature module
- **WHEN** an agent adds a feature, layer, or project
- **THEN** the guidance instructs it to inspect existing namespaces, assemblies, references, classification conventions, and policy coverage before choosing a role or policy selector

#### Scenario: Agent adds a bounded context
- **WHEN** an agent adds a Sales- or Inventory-like bounded context
- **THEN** the guidance shows how to classify the context, constrain cross-context edges with contextual contracts, and add coverage so new unmapped code is visible

#### Scenario: Agent handles a legacy migration
- **WHEN** existing code does not yet satisfy the target role model
- **THEN** the guidance instructs the agent to use audit discovery and narrow migration records before moving stable boundaries into strict gates

### Requirement: Explainable diagnostics and feedback loops
The documentation SHALL define diagnostics that are actionable for humans and AI agents and suitable for human-readable output, JSON output, CI artifacts, and iterative coding-agent feedback loops.

#### Scenario: A role is ambiguous or uncovered
- **WHEN** classification conflicts, missing metadata, an uncovered role, or an unmapped namespace is reported
- **THEN** the guidance identifies expected role, actual role, evidence/source, relevant context, coverage state, and a suggested code move or narrow policy action, while preserving the distinction from dependency violations

#### Scenario: A CI result is consumed by an AI agent
- **WHEN** an agent reviews a multi-file change using CI or JSON output
- **THEN** the guidance tells it to compare coverage deltas, new semantic contexts, uncovered namespaces, new cross-context edges, and classification evidence before proposing edits

### Requirement: Safe exceptions and fail-closed governance
The documentation SHALL require narrow, reasoned exceptions and SHALL warn against broad overrides, unrestricted regex, blanket ignores, and automatic policy weakening; ambiguous or uncovered architecture roles SHALL remain reviewable debt until classified or explicitly excluded with a reason.

#### Scenario: Shared kernel exception is needed
- **WHEN** Sales and Inventory share an intentional SharedKernel module
- **THEN** the guidance shows a narrowly selected SharedKernel role and contextual allow-list or exclusion with a concrete reason, without granting a broad cross-context bypass

#### Scenario: Classification is ambiguous
- **WHEN** code matches conflicting conventions or no governed role
- **THEN** the guidance directs the agent to fail closed for governance purposes, preserve the diagnostic, and request classification or a reviewed narrow exclusion rather than guessing

### Requirement: Required architecture examples and static-analysis boundary
The documentation SHALL include examples for a Sales/Inventory/SharedKernel modular monolith and a Unity/client-style fast-growing feature layout, and SHALL explicitly state that semantic-role guidance remains static analysis and does not prove runtime DI behavior, security correctness, runtime monitoring, or semantic data flow.

#### Scenario: Modular monolith example is reviewed
- **WHEN** a reviewer validates the Sales/Inventory/SharedKernel example
- **THEN** the example uses current namespace or classification selectors, contextual contracts, safe shared-kernel treatment, and semantic-role coverage without unsupported YAML

#### Scenario: Unity/client example is reviewed
- **WHEN** a reviewer validates the Unity/client-style example
- **THEN** the example uses namespace or metadata facts for runtime/editor and feature boundaries, explains asmdef limits, and does not claim scene or runtime behavior inspection
