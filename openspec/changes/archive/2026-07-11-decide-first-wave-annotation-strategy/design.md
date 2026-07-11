## Context

The catalog describes optional annotation names, while #108 owns any package strategy. This change closes the decision gap by fixing the first-wave stance without pre-empting #108's implementation design.

## Goals / Non-Goals

**Goals:**

- Make the absence of built-in annotations in the first wave explicit.
- Preserve custom full-name YAML mappings as the supported adoption path.

**Non-Goals:**

- No new annotation project, NuGet package, source generator, binary reference, or package-format decision.

## Decisions

The first semantic-role-catalog wave approves no ArchLinterNet-provided annotation types. Names such as `DomainLayer` and `SharedKernel` are catalog candidates/examples only. Issue #108 must separately decide whether an optional convenience distribution exists and, if so, its packaging strategy.

## Risks / Trade-offs

- [Risk] Readers can mistake example annotation syntax for product API. → Mitigation: label it candidates-only and pair it with user-defined YAML mappings.
