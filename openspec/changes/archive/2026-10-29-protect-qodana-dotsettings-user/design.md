# Design

## Context

The publisher checks changed file basenames case-insensitively and fails the Qodana status for protected analysis inputs. ReSharper's user-layer files append `.user` to a `.DotSettings` filename, so the current suffix test does not match them.

## Goals / Non-Goals

**Goals:**

- Match nested `.DotSettings.user` files regardless of case.
- Keep matching scoped to the complete suffix so unrelated `.user` files remain eligible for normal analysis.

**Non-Goals:**

- Change Qodana scan policy, baseline logic, permissions, or required status rollout.

## Decisions

- Add an explicit `.dotsettings.user` basename suffix check before the existing `.dotsettings` check. The path is already reduced to its basename and lowercased, so this protects nested paths and remains case-insensitive.
- Test both positive matching and nearby negative examples, and verify that the trusted governance projection reports a changed user-layer file.
- Keep the `.DotSettings` rule unchanged for standard ReSharper settings files.

## Risks / Trade-offs

- **A ReSharper settings naming variant could use a different suffix** → The regression contract covers the reported `.DotSettings.user` form and preserves the existing standard suffix rule.
