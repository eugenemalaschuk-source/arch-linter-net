## Context

The existing Core projector retains canonical Health dimensions and reasons, while the CLI renders
only fail/unassessable reasons under `Blockers`. The waiver aggregate excludes
`metadata_incomplete` and `invalid` from the compact headline, and normal navigation is passed
through the same `max-details` limit as findings. The CI producer already owns trusted repository,
head, run, and attempt values but must remain a read-only renderer/transport boundary.

## Goals / Non-Goals

**Goals:**

- Preserve Core as the only owner of report facts and preserve independent Gate and Health.
- Make advisory reason codes visible without upgrading them to blockers.
- Provide bounded Markdown with an unbounded-by-detail, allowlisted navigation link to the exact
  CI artifact bundle.
- Cover the #799 `PASS`/`DEGRADING` waiver-metadata shape with a public-safe fixture.

**Non-Goals:**

- Recompute Health, lifecycle, policy weakening, or applicability in Markdown or workflow glue.
- Alter Health/Gate semantics, run untrusted PR code in the publisher, or add arbitrary external
  navigation URLs.
- Change historical diagnostic/SARIF aggregation owned by #784.

## Decisions

1. Add a Core projection field for classified dimension explanations, rather than re-filtering
   dimension reasons in the CLI. The field carries the canonical state, reason code, identities,
   and whether the state is blocking; this leaves Markdown presentation as an adapter and allows
   future consumers to preserve the same explanation. Existing dimension arrays remain compatible.

2. Render `Blockers` from only the classified blocking explanations and authorities that already
   define a blocking condition. Render a separate `Health explanation` section for all remaining
   non-healthy canonical dimensions. This directly separates `PASS + DEGRADING` and `PASS + DEBT`
   from failure while keeping a failing report explanatory.

3. Use canonical `ArchitecturePolicyInventoryIgnoreDebt` counts as the full lifecycle summary,
   including metadata-incomplete and invalid. Detail remains bounded and deterministically sorted;
   counts are never derived from the displayed rows.

4. Extend the `report pr` input with an optional `--artifact-url` transport value. It is accepted
   only as a well-formed HTTPS GitHub Actions URL bound to the configured repository/run path; the
   Core projection records it as transport navigation, never as evidence. The renderer emits it in
   an always-visible immutable-evidence section. Local invocations omit it without fabricating a
   link. The workflow constructs it from its trusted context; the publisher validates and moves
   rendered bytes exactly as before.

5. Keep writing serial for the coupled Core/CLI report contract and workflow. A separate worker
   can own the public-safe fixture/documentation and a second can own isolated publisher/transport
   test additions only after the contract is established.

## Risks / Trade-offs

- [A user-controlled URL could become a misleading link] → accept only an allowlisted HTTPS GitHub
  Actions run/artifact form and escape it as a Markdown destination.
- [Additional report content can exceed comment limits] → concise reason rows and existing bounded
  details; only one explicit transport link is unbounded by count.
- [Historic Health input lacks the new transport context] → preserve available canonical report
  rendering and label the full bundle link unavailable rather than inventing one.

## Migration Plan

Deploy Core/CLI and workflow changes together. Existing artifacts remain readable, with no
transport link. Rollback is a normal code revert; the sticky publisher continues to publish only
validated manifest-bound report bytes.
