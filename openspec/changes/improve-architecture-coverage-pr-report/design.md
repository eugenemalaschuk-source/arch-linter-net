## Context

The strict JSON artifact already contains normalized, contract-aware findings in `violations`,
`coverage_findings`, and related diagnostic arrays. The PR Markdown generator currently consumes
only `coverage_summary`, so a failing comment hides the direct evidence that triggered the gate.

## Goals / Non-Goals

**Goals:**

- Make a failed sticky PR comment actionable without downloading an artifact.
- Preserve deterministic ordering and bounded comment size.
- Make the aggregate table distinguish failed rules from failed diagnostics.

**Non-Goals:**

- Change strict/audit evaluation, baseline semantics, JSON/SARIF schemas, or GitHub Actions
  permissions.
- Reproduce every raw JSON field or add links to source files outside the existing locations.

## Decisions

1. The Python generator will collect structured failure arrays, group by `contract_id`, and use a
   readable category only when the JSON finding has no contract. This preserves stable identifiers
   while retaining actionable preflight and classification failures.
2. The detailed Markdown artifact will render all diagnostics. A second, compact Markdown output
   will show a bounded, sorted set of representatives and an omitted count. Both render the full
   diagnostic count, so the comment does not conceal the scale of a failure.
3. If structured coverage findings are absent, problem entries in `coverage_summary` will be
   synthesized as fallback evidence. This handles partial/older producer output without masking
   a failing coverage gate.
4. The status header and Failed rules section precede the existing aggregate and new-code tables;
   the existing tables remain secondary operational context. The workflow appends a link to its
   run artifacts when posting the compact output.
5. Repair policy-integrity failures before treating resulting audit diagnostics as source
   violations. Then remediate violations in governed C# code, linter tooling, and CI inputs, while
   preserving the declared policy semantics.

## Risks / Trade-offs

- [An unfamiliar diagnostic shape lacks preferred fields] -> render known generic fields and a
  category fallback instead of failing the comment generator.
- [Large failure sets make comments noisy] -> bound representatives per rule in the comment,
  sort all groups, show omitted counts, and link to the full artifact.
- [Coverage fallback could duplicate structured diagnostics] -> use it only when the structured
  `coverage_findings` array is absent or empty.
- [A policy repair masks a real violation] -> keep role classification and coverage input sets
  explicit, then run strict validation to expose and fix the resulting diagnostics.

## Migration Plan

Deploy with the PR. The next Architecture Coverage CI run updates the existing sticky comment in
place; rollback is a normal code revert with no persisted data migration.

## Open Questions

None.
