## Context

The initial #827 implementation already owns profile semantics in the CLI and
validity evidence in Core. Review found a mismatch between the CLI validator,
the packaged JSON schema, and the unavailable fixture, plus a Windows-only
test launcher and generic strict-failure output.

## Goals / Non-Goals

**Goals:** align the executable allow-list with the shipped schema and fixtures,
preserve actionable strict rejection reasons, and make the pack/install test
portable.

**Non-Goals:** changing Core Health identity, adding relay deployment, or
changing public .NET APIs.

## Decisions

- Treat the shipped schema's count grammar as the product allow-list: exact
  representation comparison alone is insufficient when the projector can form
  dynamic counts.
- Keep rejection reasons on the internal projection and write them only for
  explicit strict-profile calls. This retains legacy payload compatibility while
  making strict failures actionable.
- Select the installed tool file name using the runtime platform rather than
  invoking a platform shell or adding a test-only wrapper.

## Risks / Trade-offs

- [New strict rejection for counts above 9999] → strict profiles fail closed
  with a diagnostic; legacy badges retain their existing output.
- [Tool packaging layout varies by platform] → the test asserts behavior only
  through the platform-correct command name installed by `dotnet tool`.
