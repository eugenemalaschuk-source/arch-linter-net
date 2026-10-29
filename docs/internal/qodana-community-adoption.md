# Qodana Community .NET quality gate

The Qodana pull-request gate is designed to be required alongside the existing Sonar and CI checks. The scan runs independently in `.github/workflows/qodana.yml` with read-only permissions and no secrets. A separate `.github/workflows/qodana-publisher.yml` runs from the default branch after the scan completes, validates the exact-run evidence, evaluates the finding delta, updates one bounded PR report, and publishes the commit status context `qodana`. The versioned Main ruleset payload now requires that context; apply it after the publisher is present on the default branch, as described in [required status checks inventory](required-status-checks-inventory.md).

The Qodana scanner uses the free Community .NET image `jetbrains/qodana-cdnet:2026.2`, pinned by tag and manifest digest in [qodana.yaml](../../qodana.yaml). This is the current documented Community .NET release at the time of this change. The scanner analyzes `ArchLinterNet.slnx` with `qodana.recommended` and the locked .NET SDK. The scan workflow has no write permission, no Qodana Cloud token, no privileged container, no Docker socket mount, and no automatic fixes.

## Gate policy

The scanner analyzes the exact pull-request base and head in separate checkouts and caches. The trusted publisher compares their SARIF reports, so a previously fixed finding cannot be reintroduced simply because an older committed baseline still lists it. Findings first match by exact identity, then by Qodana's fingerprint, rule, path, message and severity; repeated fingerprints are counted separately. The publisher also reads the reviewed policy in `tools/qodana/baseline.json` from the exact base SHA. That historical inventory was generated from main revision `dbc49a544ab8f39bc635a0628d769704945dfca5` (workflow run [40782711991](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/40782711991)); it recorded 13,280 Moderate diagnostics and no High or Critical findings. It is not a Qodana configuration suppression.

The verdict rules are:

- Every High or Critical finding fails, including a severity increase on an existing finding.
- Every new Moderate finding fails, covering all Qodana inspections that report ordinary code-quality or maintainability issues.
- New Low and Info findings are included in the report and remain visible but do not block.
- Findings removed since the base scan are reported as remediated.
- A missing, malformed, failed, stale, or identity-mismatched scan fails closed as `UNASSESSABLE`.
- The comparison uses the freshly scanned PR base; the reviewed policy comes from the base revision, so a PR cannot approve its own changes.
- A baseline refresh must be the only changed path in its PR. The candidate must exactly match the successful base SARIF inventory, stamped with the base SHA because the source under analysis is unchanged from that base. The current verdict compares fresh head and base scans; the refreshed inventory becomes a reviewed record after merge. Mixing a baseline refresh with source or policy changes fails closed.
- Changes to scanner, profile, suppression, or build inputs are not assessed by the scan they modify. The publisher reports these paths and fails closed. Once `qodana` is required, merging an approved change to those inputs requires an authorized ruleset bypass after a separate trusted review; splitting such a change into another PR does not by itself make the check pass.

The comment is identified by `<!-- archlinternet-qodana-report -->` and updated in place. It shows the verdict, severity totals, new and remediated findings, rule IDs, source locations, exact head/base identities, scan attempt and artifact link. It lists at most 25 entries per delta and has a hard 60 KB size limit. Untrusted diagnostic text is escaped to one line with mentions neutralized. The compact commit status is published to the scanned PR head under exactly `qodana`.

## Trust boundary

The scanner checks out the PR head and exact base only in a tokenless, read-only job, and scans the base first with a separate cache. Its evidence artifact is untrusted input to the publisher. The publisher fetches its script from this repository at the default-branch workflow revision; it never checks out or executes the PR head, and it does not trust the artifact's computed verdict. It validates workflow path, repository, run ID and attempt, open PR identity, current head/base, artifact name and contents, both evidence digests, scanner image/profile/solution and SARIF structure, then recomputes the verdict from both raw SARIF reports. PR paths that could alter analysis or suppress findings are treated as governance changes and require separate review.

These constraints follow GitHub's guidance to avoid running pull-request code in privileged `workflow_run` jobs and to treat artifacts from earlier untrusted workflows as untrusted data ([secure use of GitHub Actions](https://docs.github.com/en/actions/reference/security/secure-use), [workflow events](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows)).

## Required-check rollout

The versioned [Main ruleset payload](main-ruleset-required-status-checks.json) contains all existing required contexts plus `qodana`, increasing the required list from 15 to 16. The active Main ruleset must be updated only after this `workflow_run` publisher is present on the default branch, because GitHub only triggers `workflow_run` workflows that exist on the default branch. The repository ruleset change remains a maintainer operation; after applying the payload, verify the active ruleset contains `qodana` and that a missing or failed status blocks merge.

The scanner check itself is intentionally not the required context: it proves Qodana could run and preserve evidence. The default-branch publisher's `qodana` status is the finding-level gate.

## Updating Qodana or its baseline

The current image pin is `2026.2` with manifest digest `sha256:138560f97271952d6a9511866db99ac63b45673416e7938442a807202e3ce4f2`. Review the official [Qodana .NET documentation](https://www.jetbrains.com/help/qodana/dotnet.html) before updating it. Run a full canonical scan, inspect the SARIF, and build a new baseline only from a successful, exact-revision scan:

```bash
python3 tools/scripts/qodana_gate.py \
  --build-baseline /path/to/cold.sarif.json \
  --analyzed-sha <full-scan-sha> \
  --output tools/qodana/baseline.json
```

Baseline generation refuses High/Critical findings. The differential gate blocks every new Moderate finding and does not use a per-rule allowlist. A baseline refresh PR must change only `tools/qodana/baseline.json`; its candidate is verified against a successful scan of the unchanged base source and stamped with that base SHA. The gate compares fresh base and head reports, so the update cannot hide new findings in its own PR. The refreshed inventory is a reviewed record after merge. Do not add blanket exclusions or unreviewed suppressions.

## Current Sonar inventory

The exact-main Sonar analysis used during this quality pass is [workflow run 40782135609](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/40782135609), on the same revision as the Qodana inventory above. It passed Sonar's Quality Gate with zero bugs, vulnerabilities, and security hotspots. The maintainability inventory contained 214 untriaged code smells, including 1 Blocker, 22 Critical, and 38 Major findings; the implementation change records the fixes and remaining accepted API compatibility cases separately. Re-run the repository's Sonar inventory after remediation before closing this work.
