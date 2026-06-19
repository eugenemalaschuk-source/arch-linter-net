# Pull Request Governance

Pull requests must preserve one-to-one traceability with the backlog issue they implement.

## Title rule

When a pull request implements a GitHub issue, use the issue title verbatim and append the issue number in parentheses.

Example:

```text
Issue: [TASK][AI] Tooling: Document release publication workflow
PR:    [TASK][AI] Tooling: Document release publication workflow (#29)
```

Do not replace the issue title with a conventional-commit style PR title such as `docs(release): ...` or `feat(...): ...`.

## Body link rule

The pull request body must link the implemented issue explicitly:

```text
Closes #29
```

Use `Fixes #...` or `Resolves #...` only when that wording is more accurate.

## Multiple issues

Prefer one issue per pull request. If a pull request intentionally covers multiple issues, use the primary issue title plus its issue number in the PR title and list every closed or related issue in the body.
