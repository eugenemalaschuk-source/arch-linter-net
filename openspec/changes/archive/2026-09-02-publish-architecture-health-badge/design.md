## Context

The read-only `Architecture Coverage` PR job already creates canonical Health JSON and a
manifest-bound PR report, while the legacy `badge architecture-policy` command merely projects a
strict validation result. `main` intentionally runs focused coverage/Sonar/Codecov telemetry only;
it cannot be used to recalculate Architecture Health. See proposal.md for motivation and the delta
specifications for observable behavior.

## Goals / Non-Goals

**Goals:**

- Keep Health, policy inventory, message construction, and color selection in the CLI.
- Bind the generated payload to one required PR producer and prove its Git tree is the merged
  tree before publishing it.
- Make unavailable promotion evidence explicitly non-green at the same stable endpoint.

**Non-Goals:**

- Create another Health/inventory evaluator, count policy YAML in CI, or revive the old workflow
  status badge.
- Rerun architecture validation, deploy Pages, or change release/package publication on `main`.
- Change the legacy strict `architecture-policy` badge's compatible behavior.

## Decisions

1. **Project the health badge in the existing Badge CLI command family.** The command parses only
   the versioned Health artifact and its selected inventory receipt, then centrally maps the typed
   Health value to Shields fields. This meets the issue's Core/CLI ownership boundary and reuses
   the established stdout payload convention. An alternative workflow-script formatter would
   duplicate product semantics and is rejected.

2. **PR CI produces a separate small immutable badge artifact.** It contains only the exact CLI
   JSON and a fixed-schema manifest with PR/base/run/head/tree/hash binding. It is distinct from
   the PR Markdown artifact so its publisher can enforce a small, purpose-specific transport
   protocol. Reusing the Markdown manifest would fail to bind the badge bytes or tree identity.

3. **A push-to-main publisher proves tree equality, not commit equality.** It resolves the merged
   PR associated with the pushed commit, verifies the PR base/merge context and required producer,
   loads both commit objects, and compares their Git-tree IDs. This supports squash merge while
   rejecting stale heads and different contents. Matching commit SHA alone is rejected because it
   cannot establish the intended PR-evidence relationship.

4. **Publish to an automation-owned `architecture-health-badge` branch.** The stable raw GitHub
   endpoint is `architecture-health.json` on that branch, with a separate bounded metadata JSON.
   The publisher uses the fixed GitHub contents API path, serializes writers through workflow
   concurrency, and never writes `main`. This avoids an always-on service and ordinary Pages
   deployment. A missing branch is created once from trusted `main` before the fixed files are
   written.

5. **Fail closed with the CLI's explicit unassessable projection.** If ready evidence cannot be
   proven, the trusted main job runs only the badge CLI against an unavailable input to generate
   the standard `UNASSESSABLE · ? ignores · ? rules` payload, then overwrites the endpoint and
   writes a reason-only metadata record. It does not rerun Health or architecture analysis. An
   inline workflow fallback would risk color/message semantic drift; leaving the old payload would
   falsely represent historical evidence as current.

6. **Treat all downloaded bytes as inert.** The privileged workflow validates closed artifact file
   names, regular-file shape, size limits, UTF-8 JSON schema, manifest context, and SHA-256 before
   publishing the payload. It performs no PR checkout and does not interpolate artifact data into
   executable shell or workflow source. Fixture-driven Node harness tests exercise the trusted
   resolver, validator, fallback, and static-branch writer protocol.

## Risks / Trade-offs

- [GitHub cannot associate a pushed squash commit with exactly one merged PR] → publish the
  CLI-generated unassessable payload and retain a bounded reason in metadata.
- [Artifact retention or API permissions prevent evidence retrieval] → fail closed at the stable
  endpoint rather than show the previous ready payload.
- [Static branch is absent or concurrent writers race] → create it from trusted main once and use
  a fixed-path, serialized, optimistic contents update.
- [The main workflow's CLI cannot build for the fallback] → fail the publisher after attempting to
  overwrite the endpoint only with a valid CLI payload; never retain or claim old healthy state.

## Migration Plan

1. Ship the CLI projection and tests.
2. Extend the read-only PR producer with its payload/manifest artifact.
3. Add and fixture-test the trusted main publisher, then verify it against a merged PR.
4. Replace the README image with the static endpoint and document its limited proof.
5. Roll back by reverting the feature branch; the endpoint then becomes unavailable until a
   compatible publisher succeeds, rather than reverting to the misleading legacy workflow badge.
