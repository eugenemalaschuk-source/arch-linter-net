# Claude Code Instructions

@AGENTS.md

## GitHub feature implementation routing

When the current user message contains exactly one GitHub issue URL matching:

```text
https://github.com/<owner>/<repository>/issues/<number>
```

invoke the `feature-implementation` skill and pass the issue URL as its input.

Do not ask the user to repeat the workflow or confirm that implementation should begin. This rule does not apply to pull request URLs. Do not merge the resulting pull request unless explicitly requested.

## Release preparation routing

When the current user asks to prepare, ready, close, or otherwise assemble repository-side content/authority for a concrete ArchLinterNet release, invoke the `release-preparation` skill.

Treat an explicit version as the release target. A request for the next patch/minor/major/preview release may derive the target only through the repository release-process rules and current release/tag facts.

Do not interpret release preparation as implicit permission to publish packages, create a tag/GitHub Release, or deploy release docs. Follow the maintainer publication boundary in `docs/reference/release-process.md`.
