## Context

Checkpoint A already owns the reusable synthetic fixtures and #411 has
reconciled the 0.5.1 contract. Existing PR package validation verifies selected
package contents, but its shell implementation is Linux-only and does not
produce the release decision record required by #366.

## Goals / Non-Goals

**Goals:**

- Run a candidate pack and isolated-feed consumer matrix from an NUnit
  entrypoint that reuses `AdoptionAcceptanceFixture`.
- Validate the installed CLI, Core/CEL/Testing package identities and embedded
  registry resources without source-project references in consumer paths.
- Make platform evidence portable by placing platform-neutral assertions in
  .NET tests and running them from a release workflow matrix.
- Produce stable JSON/Markdown evidence only after all local matrix assertions
  succeed.

**Non-Goals:**

- Implement product features, publish packages, create a private adopter, or
  turn GitHub Actions into a product requirement.
- Claim an unobserved platform result in checked-in release evidence.

## Decisions

- Use an NUnit E2E fixture rather than a second shell fixture system. It can
  copy existing synthetic roots, pack from the checkout, invoke the installed
  tool with redirected streams, and create external consumer projects with a
  local-only NuGet configuration. This keeps reusable corpus ownership with
  #403.
- Pass the candidate version through `CHECKPOINT_B_CANDIDATE_VERSION`; default
  it to `0.5.1` for the release gate. Packaging with that value makes all four
  package identities comparable while avoiding a source-version bump solely
  for an ephemeral candidate.
- Run the portable fixture on Linux x64, Windows x64/PowerShell, macOS arm64,
  and macOS x86_64 in a dedicated workflow. Each runner packs independently;
  candidate identity is commit plus version, not byte-for-byte cross-platform
  package equality.
- Write release evidence only in the release workflow's aggregation job from
  per-platform JSON artifacts. This avoids falsely checking in successful
  platform claims before the matrix has run. The final evidence is uploaded as
  a release gate artifact and must be reviewed before publication.
- Keep package archive and schema assertions in the portable test harness so
  they supersede Linux-specific shell assumptions. The existing PR package
  validation remains useful fast feedback.

## Risks / Trade-offs

- [Runner images do not expose a required architecture] → Pin the required
  macOS runner labels and fail the gate if one cannot run; do not substitute an
  emulated result.
- [Network access leaks into offline checks] → Create a local `NuGet.Config`
  that clears all sources and use `--ignore-failed-sources`; schema discovery
  itself never restores or contacts a source.
- [A package install resolves a project output] → Run the consumer in an
  isolated temporary directory, use only the copied local feed, and assert
  package asset paths before executing it.
- [Evidence becomes stale] → Include commit SHA, candidate version, platform,
  runtime, package digests, and scenario outcomes in every generated record.

## Migration Plan

1. Add the portable packed-candidate acceptance fixture and its focused tests.
2. Add the Checkpoint B workflow matrix and artifact aggregation.
3. Run the local macOS observation and all repository gates.
4. Archive the OpenSpec change, then run the dispatch workflow against the PR
   commit to obtain the final cross-platform evidence before publishing.

## Open Questions

- The final publication run must supply the release commit SHA to the evidence
  aggregation job; this is resolved by the workflow environment rather than a
  checked-in placeholder record.
