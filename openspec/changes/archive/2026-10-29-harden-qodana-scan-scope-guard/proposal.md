# Harden Qodana scan-scope change detection

## Why

Review of PR #1119 found that a pull request could change ReSharper `.DotSettings` or `.gitignore` inputs without the trusted publisher requiring separate review. Removing a ReSharper restore directive could also extend an existing suppression while the gate inspected only added lines.

## What Changes

- Treat all `.DotSettings` files and root or nested `.gitignore` files as trusted scan-scope inputs.
- Reject patches that add suppressions or delete a restoration directive that bounds suppression scope.
- Add contract tests for the protected paths and deleted restoration directives.

## Non-goals

- Change Qodana severity thresholds, baseline matching, scan workflow permissions, or the required-status rollout.
- Allow scan-policy changes to pass through this gate without a separately reviewed ruleset bypass.
