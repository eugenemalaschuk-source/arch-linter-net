## ADDED Requirements

### Requirement: Dual-mode CI and adoption guidance uses one combined invocation

The evergreen CI, adoption, upgrade, reference-entrypoint, and output guidance SHALL recommend one `--mode strict,audit --ensure-built` invocation when a workflow requires strict and audit results from the same build state. The guidance SHALL show combined JSON and SARIF report routing where artifacts are needed, state that the command fails when either requested mode fails, and explain that additional report sinks reuse the completed analysis. It SHALL retain separate strict and non-blocking audit examples for workflows that intentionally treat audit as advisory, and SHALL NOT claim prepared-state reuse across independent CLI processes.

#### Scenario: A team needs strict and audit as one required CI decision

- **WHEN** a maintainer follows the documented dual-mode CI path
- **THEN** the workflow invokes the CLI once with `--mode strict,audit
  --ensure-built`, preserves both result views in its artifacts, and receives
  the aggregate command exit category
