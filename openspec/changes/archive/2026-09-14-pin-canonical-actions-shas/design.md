## Context

See proposal.md - Why. Production `.github/workflows/*.yml` already pin third-party actions to
full commit SHAs (see `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1`,
`actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68`,
`actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a`). The existing docs-lint
tooling pattern (`tools/scripts/check_evergreen_docs.py`, `check_dogfood_reference_evidence.py`,
`check_public_docs_contract.py`) is a small deterministic Python script under `tools/scripts/`,
wired into `make/docs.mk`'s `lint-docs` target, with a pytest regression file under
`tools/scripts/tests/`.

## Goals / Non-Goals

**Goals:**
- Re-pin the three canonical docs files' mutable `uses:` refs to the same commit SHAs already
  reviewed for the same action/version in production workflows.
- Add a deterministic lint that scans Markdown code fences tagged `yaml`/`yml` for `uses:` lines
  and fails on a mutable third-party ref.

**Non-Goals:**
- Rewriting archived OpenSpec examples or other historical/non-canonical prose.
- Building a general-purpose YAML/Actions parser; a line-oriented scan of fenced code blocks is
  sufficient for the fixed set of canonical docs.
- Changing production workflow files themselves (already compliant).

## Decisions

- **Follow the existing `check_*.py` pattern** rather than extending `check_public_docs_contract.py`:
  this is a narrower, independently testable concern (Actions pinning vs. capability-inventory
  truth), matching how `check_evergreen_docs.py` and `check_dogfood_reference_evidence.py` are
  already separate scripts each wired into `lint-docs`.
- **Scope detection to fenced code blocks** (` ```yaml` / ` ```yml`) inside `docs/**/*.md`, since
  the issue explicitly distinguishes canonical workflow snippets from prose mentioning `@v4`
  descriptively. A line matching `uses:\s+<owner>/<repo>(/<path>)?@<ref>` inside such a fence is a
  candidate; a `ref` that is not a 40-character hex commit SHA is a violation, unless the `uses:`
  target starts with `./` (first-party local action) — reusable-workflow refs to this repository
  (`eugenemalaschuk-source/arch-linter-net/...@<sha-or-branch>`) are also exempt, matching the
  existing `docs/guides/ci-integration.md` reusable-workflow example that intentionally uses a
  placeholder `<reviewed-sha>` token.
- **No new reviewed-allowlist file**: the issue asks for "an explicit reviewed allowlist only
  where an immutable pin is technically impossible." No such case exists in the current canonical
  docs (every third-party action already has a production SHA to reuse), so the lint has no
  allowlist mechanism yet; one can be added later if a real case appears, to avoid speculative
  complexity now.

## Risks / Trade-offs

- [Regex-based fence scanning could misclassify an edge-case snippet] → Mitigation: the pattern
  requires the line to be inside a fenced `yaml`/`yml` block and match a `uses:` key precisely;
  regression tests cover a mutable pin, a SHA pin, a local action, a reusable-workflow ref, and
  descriptive prose outside a fence.
- [Commit SHAs go stale as upstream actions release new versions] → Mitigation: unchanged from
  the existing production-workflow convention; not a new risk introduced by this change.
