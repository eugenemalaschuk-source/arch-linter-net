# Design: Harden Qodana scan-scope change detection

## Context

The privileged publisher does not execute pull-request code. It receives the PR file list and patches from GitHub and fails the Qodana status when a changed scan input or suppression could weaken analysis. The current path check omits ReSharper solution settings and Git ignore files, while the suppression check only recognizes added lines.

## Decisions

- Protect any changed basename ending in `.DotSettings`, case-insensitively, and every root or nested `.gitignore` path.
- Preserve the existing fail-closed behavior for protected inputs and the Qodana baseline exception.
- Inspect patch additions for suppression directives and deletions for ReSharper, Qodana, and compiler-warning restore directives. A deleted restore can widen the active suppression scope, so it requires separate review.
- Keep the finding policy, scan identity checks, and publisher trust boundary unchanged.

## Consequences

PRs that alter these analysis inputs or expand suppression scope become unassessable under the current scan and require the documented trusted review/bypass process. Ordinary source edits remain eligible for the differential scan.
