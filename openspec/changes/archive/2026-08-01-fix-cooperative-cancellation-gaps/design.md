## Context

Cancellation must retain its single Core-seam contract across report publication, child-process
execution, policy composition, and shared validation consumers.

## Goals / Non-Goals

**Goals:** complete every #375 cancellation boundary and add deterministic regression tests.

**Non-Goals:** introduce new CLI cancellation flags or alter successful validation output.

## Decisions

- Pass the existing token through public application requests and shared loaders rather than create host-specific paths.
- Check cancellation at outer work-item boundaries and immediately before externally visible publication.
- Drain async process output with parameterless `WaitForExit()` after polling.

## Risks / Trade-offs

- [API propagation] → Add optional token properties to preserve source compatibility.
- [Cancellation races] → Cover pre-render, completed-stream, and final-outcome boundaries with tests.
