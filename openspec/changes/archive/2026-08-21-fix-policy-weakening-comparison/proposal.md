## Why

Review of #119 found three paths where the policy-weakening comparison could
misclassify or miss effective policy changes. The guardrail must remain
fail-closed and prove only semantics that its typed evidence supports.

## What Changes

- Export typed ignored-violation matchers and advance policy-context JSON to v3
  so universal ignore detection never parses display text.
- Detect required-to-optional source sets and typed rule inputs as semantic
  weakening.
- Classify changed project include/exclude globs as `impact_not_proven` without
  resolved project membership, rather than treating glob strings as inventories.
- Repair the Core public API approval and exhaustive namespace fixtures.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `policy-context-export`: Export v3 typed ignored-violation matcher evidence
  required by fail-closed weakening comparison.
- `policy-weakening-guardrails`: Correct universal ignore, optional-input, and
  project-glob comparison classifications.

## Impact

Changes Core policy-context and comparison models, CLI-produced context
artifacts, public docs, API approvals, self-policy fixtures, and NUnit tests.
