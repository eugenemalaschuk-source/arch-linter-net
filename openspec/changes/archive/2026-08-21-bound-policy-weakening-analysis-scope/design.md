## Context

`ArchitecturePolicyContextAnalysis` is projected from the loaded policy before
runner setup. It does not include `analysis.solution`, discovery outputs, or a
resolved scanner root inventory. At runtime, discovery can seed targets and
roots, while an empty scanner-root input can select the default `src` and
`tests` roots.

## Decisions

- Treat any changed authored target-assembly, project, or source-root list as
  bounded `impact_not_proven` evidence.
- Do not infer path containment for source roots, project membership, or
  discovery fallback from raw strings.
- Retain semantic static-scope findings only where other context sections
  already provide explicit effective evidence, such as resolved source sets and
  source expansions.

## Non-Goals

- Add a resolved analysis-scope/context schema version in this correction.
- Run project discovery or source scanning during policy-context comparison.
