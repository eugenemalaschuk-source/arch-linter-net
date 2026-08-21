## Context

Effective context exports retain `OptionalEmpty` on source expansions and
structured contract facts. The original directional comparison did not inspect
all of those values, leaving a no-finding path for required-to-empty-tolerant
changes and other deterministically different facts.

## Decisions

- A source expansion changing `OptionalEmpty` from false to true is a known,
  directional relaxation and emits a `semantic` finding.
- An otherwise unsupported top-level typed contract-fact change emits one
  `impact_not_proven` finding containing canonical fact evidence. It makes the
  change reviewable without claiming a semantic scope result.
- Facts already handled directionally, and facts handled by the dedicated
  selector comparison, are excluded from the fallback to preserve one finding
  per evidence path.

## Non-Goals

- Infer affected architecture subjects from fallback facts.
- Establish directional semantics for every contract family.
