## Context

`release-nuget.yml` can calculate or accept a preview package version, but
`create_release_scope_evidence.py` currently validates publication targets
with a stable-only `X.Y.Z` pattern. Non-publishing preview candidates are
correctly allowed through the separate non-authorizing prepublication path,
but there is no reviewed path to publish an actual preview.

The latest stable release is `v0.8.2`. Automatic preview increment from that
tag would produce `0.8.3-preview.1`, so the first preview of the v0.9 line
must use the exact reviewed override `0.9.0-preview.1`.

## Decisions

### Extend exact target grammar only to preview.N

Publication declarations accept:

- `X.Y.Z`;
- `X.Y.Z-preview.N`.

They do not accept arbitrary SemVer prerelease identifiers, ranges, wildcards,
or caller-selected declaration paths. This keeps the existing declaration
selection model deterministic and reviewable.

### Keep stable and preview authority independent

Declaration selection remains exact string equality against the immutable
candidate manifest version. Therefore `0.9.0-preview.1` cannot authorize
`0.9.0`, `0.9.0-preview.2`, or any other candidate.

Stable v0.9 release authority (#787), final documentation gate (#650), and
remaining performance work stay open. Publishing the preview is not evidence
that their acceptance is satisfied.

### Preserve required-item fail-closed resolution

Preview declarations use the same required/excluded/delivered inventory and
the same live required-issue state resolution as stable declarations. The
preview is not a weaker authorization mode; it has a different exact target
and intentionally narrower reviewed scope.

### Keep version override non-authoritative

`version_override` only selects the candidate version when tag-based
calculation cannot express the intended first preview. Publication still
requires an exact reviewed declaration for that version. An override without
a matching declaration fails closed.

## Validation

Regression evidence must prove:

- exact preview declaration selection;
- required issue state binding;
- preview declaration cannot authorize stable;
- another preview number remains unmapped;
- unsupported prerelease shapes remain rejected;
- existing stable declaration inventory remains unchanged;
- non-publishing candidate behavior remains non-authorizing.
