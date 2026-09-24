# Single-tool architecture governance workflow

Start with [Getting Started](../getting-started/index.md) and a working
[strict CI check](ci-integration.md). This page adds a base/current review,
Architecture Health, a PR report, and a badge. It does not require every
optional contract family to be enabled.

## Choose the checks you need

| Question | Add |
| --- | --- |
| Does the code follow the intended component map? | [Declared topology](topology-review-workflow.md): capture observations, review them, then declare and verify the map. |
| Does a published signature expose implementation types? | [Contract-surface exposure](../contracts/contract-surface-exposure.md) and, where needed, [version isolation](../contracts/versioned-contract-surface-isolation.md). |
| Which existing findings are deliberately accepted? | A reviewed [finding baseline](migration-baselines.md). |
| Which individual violations are temporarily permitted? | [Structured waivers](../policy-format/structured-waivers.md), with an owner and expiry. |
| Is a component growing beyond a reviewed limit? | [Measure first, then add budgets](../policy-format/architecture-metrics.md). |
| Must another analyzer's results participate in governance? | [SARIF integration](sarif-integration.md), with a verified producer context. |
| Where did pressure accumulate over several releases? | [History forensics](history-forensics.md), separately from the PR gate. |

Topology, budgets, external evidence, coverage and waivers participate through
policy. Do not append a second invocation of every analytical command to CI
just to obtain more views of the same decision.

## Version and build prerequisites

Use the same exact CLI version for both revisions. The package version in the
candidate's committed `.config/dotnet-tools.json` is the pin; entering the base
worktree must not select its older tool manifest.

`health --change-snapshot` is available in `0.9.0-preview.1`; it is absent from
`0.8.2`. It produces Health and the current change snapshot in one analysis
session. The example checks the installed command's help and uses a separate
current snapshot only for older tools. A preview is an explicit adoption choice,
not a recommendation to upgrade a stable consumer without review.

This Bash example requires Git, Python 3, the CLI's .NET runtime and the SDKs
needed to build **both** selected revisions. It is for a clean, solution-backed
.NET checkout with the selected policy present at both revisions and no required
external SARIF input. Unity/staged builds and policies requiring external
artifacts need their own compatible evidence preparation; do not omit required
inputs to make this example pass. See [SARIF integration](sarif-integration.md)
and [prepared receipts](../usage/timings.md#prepared-receipts).

Build configuration, framework, platform, runtime and condition set must agree
between snapshots. The example uses each policy's normal selectors; supply the
same explicit selectors to both analytical calls when defaults differ. Do not
compare artifacts that could not be produced with compatible settings.

## Produce a review from two exact revisions

Set `BASE_SHA` to the full commit ID selected for the review and `HEAD_SHA` to
the full commit ID checked out for analysis. In GitHub Actions, distinguish the
PR head from the synthetic merge checkout: record the commit actually analyzed.
Never label merge-checkout evidence as PR-head evidence merely by changing an
environment variable. Resolve the base from the triggering event, not a later
moving `origin/main`.

Run the following from that clean candidate checkout. `POLICY` and `BASELINE`
are repository-relative paths; adjust them before running. An absent baseline
means **you deliberately selected zero accepted finding debt**, not that a
missing reviewed file may be ignored. Relative metric budgets still require
real scalar baseline entries.

<!-- example: governance-review -->
```bash
set -euo pipefail
: "${BASE_SHA:?Set the exact review-base commit ID}"
: "${HEAD_SHA:?Set the exact analyzed candidate commit ID}"
POLICY=${POLICY:-architecture/arch.yml}
BASELINE=${BASELINE:-architecture/baseline.arch.yml}
ROOT=$(git rev-parse --show-toplevel)
cd "$ROOT"
for sha in "$BASE_SHA" "$HEAD_SHA"; do
  [[ "$sha" =~ ^([0-9a-f]{40}|[0-9a-f]{64})$ ]] || exit 2
  [[ $(git cat-file -t "$sha") == commit ]] || exit 2
done
[[ "$BASE_SHA" != "$HEAD_SHA" ]] || exit 2
[[ $(git rev-parse HEAD) == "$HEAD_SHA" ]] || exit 2
git diff --quiet
git diff --cached --quiet
[[ -z $(git ls-files --others --exclude-standard) ]] || exit 2

ARTIFACTS="$ROOT/artifacts/architecture-review"
[[ ! -e "$ARTIFACTS" ]] || { echo 'Choose a fresh artifact directory.' >&2; exit 2; }
mkdir -p "$ARTIFACTS"
TEMP=$(mktemp -d)
BASE_WORKTREE="$TEMP/base"
cleanup() {
  git -C "$ROOT" worktree remove --force "$BASE_WORKTREE" >/dev/null 2>&1 || :
  rm -rf -- "$TEMP"
}
trap cleanup EXIT

git worktree add --detach "$BASE_WORKTREE" "$BASE_SHA"
VERSION=$(python3 - <<'PY'
import json
from pathlib import Path
manifest = json.loads(Path('.config/dotnet-tools.json').read_text())
print(manifest['tools']['archlinternet.cli']['version'])
PY
)
dotnet tool install ArchLinterNet.Cli --tool-path "$TEMP/tool" --version "$VERSION"
CLI="$TEMP/tool/arch-linter-net"
[[ -f "$CLI" ]] || CLI="$CLI.exe"
"$CLI" --version > "$ARTIFACTS/tool-version.txt"
printf 'base=%s\nhead=%s\n' "$BASE_SHA" "$HEAD_SHA" > "$ARTIFACTS/revisions.txt"

(
  cd "$BASE_WORKTREE"
  "$CLI" policy context --policy "$POLICY" --format json > "$ARTIFACTS/policy-base.json"
  baseline_args=()
  [[ ! -f "$BASELINE" ]] || baseline_args=(--baseline "$BASELINE")
  "$CLI" change snapshot --policy "$POLICY" --mode strict \
    "${baseline_args[@]}" --ensure-built --output "$ARTIFACTS/base.json"
)
"$CLI" policy context --policy "$POLICY" --format json > "$ARTIFACTS/policy-current.json"
CURRENT_BASELINE="$ROOT/$BASELINE"
if [[ ! -f "$CURRENT_BASELINE" ]]; then
  CURRENT_BASELINE="$ARTIFACTS/empty-baseline.yml"
  printf 'version: 3\nbaseline: {}\nmetric_baselines: []\n' > "$CURRENT_BASELINE"
fi

health_help=$("$CLI" health --help)
current_args=()
if [[ "$health_help" == *'--change-snapshot'* ]]; then
  current_args=(--change-snapshot "$ARTIFACTS/current.json")
else
  "$CLI" change snapshot --policy "$POLICY" --mode strict \
    --baseline "$CURRENT_BASELINE" --ensure-built --output "$ARTIFACTS/current.json"
fi
health_exit=0
"$CLI" health --policy "$POLICY" --baseline "$CURRENT_BASELINE" \
  --base-context "$ARTIFACTS/policy-base.json" \
  --current-context "$ARTIFACTS/policy-current.json" \
  --mode strict --ensure-built --execution-context "$HEAD_SHA" \
  "${current_args[@]}" --format json > "$ARTIFACTS/health.json" || health_exit=$?
printf '%s\n' "$health_exit" > "$ARTIFACTS/health.exit-code"
case "$health_exit" in 0|1|2) ;; *) exit 2 ;; esac
# Incomplete analysis may leave Health diagnostics but no complete snapshot.
[[ -s "$ARTIFACTS/current.json" ]] || exit 2

"$CLI" change report --base "$ARTIFACTS/base.json" \
  --current "$ARTIFACTS/current.json" --execution-context "$HEAD_SHA" \
  --format json --output "$ARTIFACTS/change.json"
"$CLI" report pr --health "$ARTIFACTS/health.json" \
  --change "$ARTIFACTS/change.json" --output "$ARTIFACTS/pr.md"
badge_exit=0
"$CLI" badge architecture-health --input "$ARTIFACTS/health.json" \
  --output "$ARTIFACTS/badge.json" || badge_exit=$?
# The badge preserves Health's gate status; a different status is an error.
[[ "$badge_exit" == "$health_exit" ]] || exit 2
exit "$health_exit"
```

The CLI renderers validate the supplied artifacts. A malformed Health document,
incompatible snapshot pair or failed output write must fail the command; the
script does not repair those inputs. Preserve the artifacts with an `always()`
upload in CI, including `health.exit-code`, even when the script fails.

The `health` command performs analysis and projects its decision; it is not a
renderer of a previous strict JSON file. `change report`, `report pr` and
`badge architecture-health` consume existing artifacts. A failing Health exit
is retained after rendering, not converted into success.

## Interpret the result

Start with `health.json`: `gate` answers whether the selected governance accepts
the candidate; `health` describes its state. `pass` can coexist with reviewed
debt. Required missing evidence is `unassessable`, not an empty successful run.
See [Architecture Health](../reference/architecture-health.md) for precedence,
dimensions, waiver lifecycle and policy inventory.

Read `change.json` and `pr.md` for base/current differences. Findings, finding
baseline debt, explicit waiver debt, policy weakening and metric regressions
are different evidence categories. [Repository metrics](../reference/repository-metrics.md)
add informational size and structure deltas; they do not change the gate.

## Publish without another analysis

A PR comment publisher transports `pr.md` only after verifying its source,
current head, run attempt and digest. A public badge publisher transports only
the approved badge projection, not the private report bundle. See
[CI integration](ci-integration.md#secure-unified-architecture-pr-report-publication)
and [badge adoption](badge-adoption.md).

A trusted exact-base producer can replace the base build in the example when
its artifacts are compatible and their provenance is verified. Prepared
receipts can avoid another build in a multi-command producer. Neither permits
an unverified cache or arbitrary `bin/` copy to stand in for analyzed evidence.
See [performance diagnosis](../usage/timings.md).
