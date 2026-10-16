## Context

The repository already has a strict publication authority boundary. A stable or preview release candidate selects exactly one tracked declaration from `tools/release/scopes/` by exact version. Checkpoint B binds its identity and digest to the candidate package manifest and source commit, and independently rechecks the selected declaration. The maintainer procedure then runs a non-publishing candidate, reviews its artifacts, and starts a separate publishing run that builds a new candidate.

Release preparation currently duplicates the reviewed scope in two places: an author-edited JSON declaration and per-target assertions in `test_create_release_scope_evidence.py`. The release story already describes that scope, but its text is not a machine-readable source.

## Goals / Non-Goals

**Goals:**

- Generate the existing declaration JSON from a strict machine-readable section in the already reviewed release story.
- Make generation deterministic, safe to repeat, and confined to the existing scope directory.
- Remove routine per-release editing of the JSON declaration and duplicated test inventory.
- Preserve the current release candidate identity, authority, two-run dry-run/publication procedure, evidence, and publication gates.

**Non-Goals:**

- Change the GitHub release workflow inputs or job graph.
- Combine dry-run and publication, publish automatically, or change the manual publication boundary.
- Infer required scope from milestone membership, PR labels, or issue state.
- Replace the tracked declaration as the exact publication authority.
- Change version calculation, package contents, attestations, Checkpoint B scenarios, release notes, NuGet publishing, GitHub Releases, or docs deployment.

## Decisions

### Use the release story as the authoring source

The existing release story will contain one versioned TOML `release-scope/v1` block with the exact `release_target`, `declaration_id`, comments, and `required_items`, `excluded_items`, and `delivered_items` inventories. The release story issue number is taken from the GitHub response and written into the generated declaration. The regular prose remains the human-facing explanation; the block carries only scope data already represented by the declaration schema. Scope intent is still reviewed in the release story and its preparation PR.

The generator fetches the named story through the existing `gh` CLI integration, requires exactly one supported block, validates the returned story identity and exact target, and rejects unknown keys, duplicate issues, cross-inventory duplicates, invalid item shapes, or missing reasons. It does not infer or broaden scope from labels, milestones, commits, or mutable issue states. TOML parsing uses Python's standard-library `tomllib`, so the release tooling gains no dependency.

### Generate the current tracked declaration format

The tool renders the existing `checkpoint-b-release-scope-declaration/v2` JSON deterministically and derives its output path only from the validated target under `tools/release/scopes/`. No release workflow consumer needs to change. This keeps the current exact-version selector and the candidate-bound declaration hash intact.

Generation supports a read-only preview and an explicit write operation. A second write that produces identical bytes succeeds idempotently. If a target already exists with different bytes, generation fails without modifying it; a reviewed authority is never silently replaced. Writes use a temporary sibling and an atomic hard-link creation for a new target.

### Make declaration tests schema-driven

Keep exact behavior tests for existing historical declarations where they protect intentional release decisions. Replace routine additions to the manually maintained full target list with generic tests that discover all tracked declarations and verify schema validity, unique exact targets, and deterministic generation from a representative release-story fixture. This removes the duplicated per-release test edit while still validating the generator and every tracked authority.

### Keep publication procedure unchanged

The generated JSON is committed and reviewed in the existing release-preparation PR before either release workflow run. The manual non-publishing rehearsal still creates and validates its own immutable candidate; the maintainer still reviews its artifacts; a separate `publish=true` run creates and validates a fresh immutable candidate before publication. Both runs consume the same committed declaration for the exact version. No generation command publishes, tags, uploads, or deploys anything.

## Risks / Trade-offs

- A release-story body may be malformed or contain multiple scope blocks → use a strict, versioned parser and fail without writing output.
- Structured scope still needs a human-reviewed intent source → keep required/excluded/delivered decisions in the existing release story rather than guessing them from milestones or issue metadata.
- A story can change after a declaration is generated → the committed declaration in the preparation PR remains the workflow authority; later story edits do not alter a candidate's selected bytes.
- A generated declaration may differ from an existing reviewed file → refuse the write and require the maintainer to resolve the conflict through the normal PR review.

## Migration Plan

1. Add parser/generator support and fixture-based tests without changing release consumers.
1. Update release-preparation instructions to place the structured scope block in the release story and run the generator when preparing the existing declaration PR.
1. Keep all current declarations and release workflow behavior unchanged.

Rollback consists of reverting the generator/instruction change. Existing checked-in declarations remain valid and continue to authorize candidates.

## Open Questions

None. The structured block will use the existing declaration's field names and current v2 schema semantics.
