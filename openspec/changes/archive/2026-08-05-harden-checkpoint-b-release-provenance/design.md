## Context

The current workflow packs independently on platform runners and again in the
release job. Its evidence can therefore describe files different from the files
later published, and its runner records trust unverified caller-supplied shell
labels and scenario claims.

## Goals / Non-Goals

**Goals:**

- Bind all platform validation and publication to one manifested package set.
- Prevent NuGet cache/source substitution for external consumers.
- Turn the required matrix into explicit, independently asserted outcomes.
- Fail closed when evidence or required tool provenance is incomplete.

**Non-Goals:**

- Add product features or publish a release from this change.
- Treat a dry-run artifact from a different commit as release authorization.

## Decisions

- A `prepare-candidate` job is the only job that packs. It uploads packages and
  a digest manifest; every later job downloads it and verifies it.
- Platform tests receive the downloaded feed through environment inputs rather
  than invoke `dotnet pack`. Their generated NuGet config clears sources and
  uses separate package and HTTP caches.
- A platform-specific invocation adapter executes bash/zsh/pwsh explicitly and
  reports its actual adapter id. Core scenario oracles remain in deterministic
  NUnit manifest data.
- The aggregator consumes candidate manifest, platform evidence, and a
  repository-gates result rather than manufacturing green status fields.

## Risks / Trade-offs

- [Artifact tampering or accidental replacement] → Manifest SHA-256 is checked
  before every consumer and publication.
- [Runner-specific path/shell behavior] → Each runner executes an explicit
  adapter and records observed details.
- [Long gate duration] → Pack once, reuse downloaded artifact, and make every
  missing result fail early.

## Migration Plan

1. Add manifest helpers and isolate the candidate feed in the E2E harness.
2. Introduce prepare/download/verify workflow jobs and remove later packing.
3. Add complete matrix oracles and strict evidence aggregation.
4. Validate all gates, archive the change, and run a non-publishing dry-run.
