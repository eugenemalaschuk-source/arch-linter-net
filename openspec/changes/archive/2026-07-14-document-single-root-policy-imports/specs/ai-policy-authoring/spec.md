## ADDED Requirements

### Requirement: AI agents decompose policies by concern with minimal edits
The AI policy-authoring guidance SHALL instruct agents to keep exactly one selected root, prefer focused fragments organized by architecture concern or bounded context, keep small shared settings inline when clearer, preserve globally unique contract IDs within each family and mode, and avoid editing unrelated fragments merely to reduce file count.

#### Scenario: Agent adds one bounded-context rule
- **WHEN** an AI agent adds or revises a rule owned by one bounded context
- **THEN** the guidance directs it to edit the owning focused fragment and the root import list only when necessary

#### Scenario: Agent reviews a fragmented policy
- **WHEN** an AI agent prepares a policy change for review
- **THEN** the checklist verifies graph roles, explicit schema fit, global conflict safety, narrow fragment scope, and validation through the selected root

