## Context

Project discovery already parses `.csproj` XML and feeds package dependency contracts with per-project metadata. Issue #89 extends that same static-analysis boundary to packaging and assembly metadata: selected MSBuild properties, `InternalsVisibleTo`, and project references now need to participate in contract evaluation without introducing full MSBuild evaluation or a second project parser.

The repo already carries realistic metadata to validate:
- `Directory.Build.props` defines inherited package-facing defaults such as `TreatWarningsAsErrors`, repository metadata, and version fields.
- `src/ArchLinterNet.Core/ArchLinterNet.Core.csproj` declares three `InternalsVisibleTo` entries.
- Test and production projects already use `ProjectReference`, which gives us a natural static signal for production-to-test leakage.

## Goals / Non-Goals

**Goals:**
- Reuse project discovery as the single source of parsed project metadata.
- Add one focused contract family that can express required properties, forbidden property values, allowed friend assemblies, and forbidden project references.
- Keep diagnostics deterministic and explicit enough for CI, docs, and audit mode.
- Preserve backward compatibility for existing discovery consumers and contract families.

**Non-Goals:**
- Full MSBuild evaluation, condition handling, or target graph execution.
- Automatic editing or repair of project files.
- Replacing NuGet package validation or broader release checks.
- A generic expression language for arbitrary metadata predicates in the first version.

## Decisions

### Decision: Extend `ArchitectureDiscoveredProject` instead of building a separate metadata reader
Project discovery already owns `.csproj` parsing and package reference extraction. Extending its models with project properties, friend assemblies, and project references keeps parsing logic in one place and lets contracts share the same discovered-project lookup machinery used by package governance.

Alternative considered:
- A standalone metadata scanner invoked only by project metadata contracts. Rejected because it would duplicate path resolution, `.csproj` parsing, and discovery-state validation.

### Decision: Support a selected property map with nearest `Directory.Build.props` inheritance
The issue explicitly calls out inherited project metadata, especially repo-wide defaults like `TreatWarningsAsErrors`. We can support the first version with a bounded static merge: walk the `Directory.Build.props` chain, parse scalar properties from XML, and let the closest definition win over more distant ones unless the project file overrides it.

Alternative considered:
- Project-local properties only. Rejected because it would miss the repo's real governance surface and make the diagnostics much less useful.
- Full Buildalyzer/MSBuild property evaluation. Rejected because the issue marks full MSBuild evaluation as out of scope and it would introduce avoidable complexity.

### Decision: Model project metadata governance as one contract family
Required properties, forbidden property values, friend-assembly allowlists, and forbidden project references all govern the same discovered-project unit and produce the same style of deterministic evidence. Keeping them in one `project_metadata` family avoids scattering closely related packaging and boundary rules across multiple small families.

Alternative considered:
- Separate `friend_assembly` and `project_reference` families. Rejected because it would multiply schema surface and loader plumbing for behavior that naturally scopes to the same project selection.

### Decision: Match projects by discovered project path
The issue's sample YAML uses `.csproj` paths directly, and project discovery already tracks deterministic repo-relative project paths. The contract should therefore target discovered projects by path instead of adding a second source selector abstraction.

Alternative considered:
- Matching by assembly name only. Rejected because metadata governance is fundamentally file/project oriented and path matching is less ambiguous for multi-project repos.

## Risks / Trade-offs

- [Static inheritance misses complex MSBuild conditions or imports] → Limit the first version to straightforward `Directory.Build.props` chain parsing, document the boundary clearly, and keep diagnostics explicit about the source file used.
- [One contract family could become too broad] → Keep the first version narrow to exact-value property checks, friend assembly allowlists, and forbidden project-reference path matches only.
- [Discovery model growth touches multiple tests and serializers] → Add focused parser tests and contract tests before wiring docs/schema changes.

## Migration Plan

1. Extend discovery models and parser behavior with additive fields only.
2. Add policy model, loader, executor, and diagnostics for the new contract family.
3. Update schema/docs/examples and validate the repo's own policy/docs flows.
4. Archive the change to synchronize canonical specs after tests pass.

Rollback is straightforward because the change is additive: remove the new contract family and discovery fields if needed without altering existing contract semantics.

## Open Questions

- Whether forbidden project references should support only glob/path matching in v1 or also discovered assembly-name matching. Current bias: path matching only, since that is what discovery already exposes directly.
- Which exact property keys should be called out in public docs/examples beyond the generic mechanism. Current bias: demonstrate `Nullable`, `IsPackable`, `TreatWarningsAsErrors`, and package metadata fields.
