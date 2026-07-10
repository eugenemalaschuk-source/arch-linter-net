#!/usr/bin/env bash
set -euo pipefail

curl --fail --silent --show-error --get \
  --data-urlencode 'components=eugenemalaschuk-source_arch-linter-net' \
  --data-urlencode 'pullRequest=271' \
  --data-urlencode 'issueStatuses=OPEN,CONFIRMED' \
  --data-urlencode 'sinceLeakPeriod=true' \
  --data-urlencode 'ps=100' \
  'https://sonarcloud.io/api/issues/search' \
  | jq -r '.issues[] | [.rule, .severity, .component, (.line // 0 | tostring), .message] | @tsv'
