# Design

## Context

The current `pull_request` workflow scans with read-only permissions and uploads an evidence artifact, but does not evaluate findings against a baseline or publish a required status. The `.NET` Community image is already pinned to the current `2026.2` release and immutable manifest digest. See `proposal.md` and `specs/qodana-community-ci/spec.md` for the intended behavior.

## Goals / Non-Goals

**Goals:**

- Keep source analysis tokenless and isolated from PR-write permissions.
- Make scanner failures and new actionable findings fail closed.
- Bind reports and statuses to an exact workflow run, PR, head, base, and scanner image.
- Show a concise PR summary and preserve complete bounded SARIF/log artifacts for investigation.
- Require the trusted Qodana status through the `Main` ruleset after the publisher is available on the default branch.

**Non-Goals:**

- Add Qodana Cloud, a paid token, or a new runtime dependency.
- Hide or drop diagnostics through broad exclusions, suppression, or an unreviewed baseline.
- Replace SonarCloud, CodeQL, compiler, or repository-specific checks.

## Decisions

### Separate scanner from trusted publisher

The existing scanner workflow remains read-only and uploads the raw SARIF plus bounded evidence even when scanning fails. A separate `workflow_run` publisher executes only from the default branch, downloads an artifact by exact run ID and attempt, validates its identity against GitHub's workflow and pull-request APIs, then recomputes the verdict from raw SARIF. It does not check out or execute PR code. This follows GitHub's privilege-separation guidance and the established `firstice` pattern. The publisher creates or updates one bounded PR comment and posts the required `qodana` commit status only while the PR head still matches the analyzed SHA.

The publisher verifies the workflow path and ID, repository, event, run attempt, PR number, head and base SHA, image digest, profile, SARIF schema and invocation result. Missing artifacts, duplicate artifacts, malformed data, an unsuccessful scan, or a stale head produce a visible failed or unassessable result; none produces success.

### Use a base-revision baseline and an explicit finding policy

The trusted publisher reads the reviewed Qodana baseline and severity policy from the exact PR base commit. The finding identity uses stable SARIF fingerprints when present and a deterministic rule/location/message fallback otherwise. Any High or Critical finding blocks, as does every new Moderate finding; existing findings are reported as baseline debt, while new Low and Info remain visible. The initial baseline is the exact successful current-main scan and contains no High/Critical findings. A baseline refresh must be the only changed path in its PR. Since the baseline file is not consumed by the scanner, this means the analyzed source is exactly the base tree; the candidate must match its successful SARIF inventory and carry the base SHA. The current verdict still uses the exact-base baseline, so the update applies only after merge and cannot approve its own new findings.

The repository keeps the pinned `jetbrains/qodana-cdnet:2026.2` Community image and manifest digest because `2026.2` is the current documented release; `firstice` also uses the `2026.2` release in its Community scan. A later version change requires updating the tag and digest together and reviewing a same-revision scan.

### Stage required-check activation safely

The ruleset payload and required-check inventory add the trusted `qodana` status. The active ruleset is updated only once the publisher exists on the default branch, because GitHub starts `workflow_run` workflows only when their workflow file is present on the default branch. Until that rollout point, the PR scan artifact and local validation provide review evidence; the existing Sonar and CI requirements remain unchanged.

## Risks / Trade-offs

- **Baseline identity drifts when nearby code edits change a diagnostic location.** → Prefer scanner fingerprints; use a deterministic fallback, report removed/new counts, and require review when regenerating the baseline.
- **A new maintainability rule can increase the blocking surface.** → Keep the lower-severity blocking set explicit and versioned; leave remaining low-severity findings visible but non-blocking.
- **The publisher can be delayed or unavailable.** → Treat missing or invalid evidence as failed/unassessable, retain the artifact, and publish a status only for the exact live head.
- **The trusted publisher cannot run from a PR branch before merge.** → Validate the scanner and verifier contracts in this PR, merge the workflow change through the current required checks, then require `qodana` in the active ruleset as a separate rollout step.

## Post-archive refinement

The implementation now scans both the exact PR base and head using isolated checkouts and caches. The publisher compares those two reports instead of treating the historical committed inventory as the current base scan; otherwise a finding removed after the initial inventory could be reintroduced without detection. The committed inventory remains an audit record and source of reviewed policy. Stable Qodana fingerprints match moved findings within a file, with duplicate counts retained.
