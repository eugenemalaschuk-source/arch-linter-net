## ADDED Requirements

### Requirement: Import loading failures expose stable categories
Policy loading SHALL expose a stable programmatic category for failures caused by portable-path validation, missing import targets, repository-boundary violations, authored path-case mismatches, import cycles, duplicate canonical imports, graph limits, source-role shape violations, and composition conflicts. Diagnostic text SHALL identify the relevant source or import chain without making callers parse that text to determine the category.

#### Scenario: Caller distinguishes a cycle from a duplicate import
- **WHEN** one policy load reaches an active source again and another reaches an already completed source
- **THEN** the two failures expose distinct cycle and duplicate-import categories

#### Scenario: Composition conflict is categorized
- **WHEN** two composed sources declare the same keyed definition or singleton setting
- **THEN** policy loading fails with the composition-conflict category and identifies both declarations
