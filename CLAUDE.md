# Claude Code Instructions

@AGENTS.md

## GitHub feature implementation routing

When the current user message contains exactly one GitHub issue URL matching:

```text
https://github.com/<owner>/<repository>/issues/<number>
```

invoke the `feature-implementation` skill and pass the issue URL as its input.

Do not ask the user to repeat the workflow or confirm that implementation should begin. This rule does not apply to pull request URLs. Do not merge the resulting pull request unless explicitly requested.
