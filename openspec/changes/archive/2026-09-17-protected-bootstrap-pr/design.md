> **Historical / superseded.** This #945 design was implemented by #946 and later superseded by the no-App bootstrap handoff in #963/#964. It is preserved only as historical evidence. Current product authority is the canonical spec under `openspec/specs/`; do not implement the GitHub App writer model from this archived change.

## Context

The bootstrap reusable workflow runs only from a trusted `workflow_dispatch`
context and verifies OIDC before it generates setup output. It currently stages
only managed files, then directly pushes the result to the configured base ref.
Protected branches correctly reject that update. In addition, GitHub's default
workflow token cannot add a new workflow file because it lacks the separate
GitHub App `workflows` permission.

## Goals / Non-Goals

**Goals:**

- Deliver generated output through a normal pull request without a base-ref
  bypass.
- Use a least-privilege GitHub App installation token with `contents`,
  `pull-requests`, and `workflows` write permissions solely to create the
  dedicated setup branch and PR.
- Keep the existing trusted OIDC inspection separate from that writer token.

**Non-Goals:**

- No PAT fallback, ruleset change, automatic PR merge, or consumer-code
  execution in the privileged bootstrap job.
- No change to Relay deployment or publication semantics.

## Decisions

1. The reusable workflow accepts optional, explicitly named GitHub App writer
   credentials for bootstrap only. It fails closed with an actionable diagnostic
   when they are absent. GitHub App credentials are selected over a PAT because
   their repository installation and `workflows` permission are scoped and
   revocable.
2. The job derives a deterministic dedicated branch from the immutable checked
   out base commit. It refuses an existing remote branch instead of force
   pushing or updating an unknown PR.
3. The generated commit is pushed only to that dedicated branch, then a PR is
   created against the configured base ref. The job does not merge or approve
   the PR. Ordinary consumer checks and branch protection remain authoritative.
4. The writer token is used only after the trusted setup command has completed
   and only for the declared generated paths. The OIDC token continues to be
   minted and verified through the pinned publisher workflow.

## Risks / Trade-offs

- [Owner must install/configure a GitHub App] → document the minimum scopes and
  fail closed before any branch write when credentials are absent.
- [Stale bootstrap branch] → deterministic name plus refusal prevents a retry
  from overwriting an existing review.
- [Workflow-file privilege] → scope the App to the consumer repository and use
  it only in the trusted dispatch job, never a pull-request event.
