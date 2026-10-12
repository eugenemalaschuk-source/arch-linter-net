## Context

See [proposal.md](proposal.md) for motivation and [the delta spec](specs/semantic-annotation-contract/spec.md) for the observable contract. The v1 manifest already fixes the 14 role identities, type targets, and six metadata properties. The source set currently implements layered/clean and DDD roles; the Core catalog already recognizes the reviewed identities through its existing exact-FQN resolver.

## Goals / Non-Goals

**Goals:**

- Add only the approved CQRS/messaging and web/API role source definitions.
- Preserve the shared source shape, metadata properties, and exact manifest targets.
- Prove the new attributes through the existing semantic classifier, role index, and downstream consumers.

**Non-Goals:**

- Adding roles marked custom-mapping-only or deferred, including `Mapper`, `Middleware`, and `Projection`.
- Changing the annotation manifest, Core recognition catalog, package build mechanism, or runtime dependencies.
- Inferring transport, routing, serialization, delivery, or public-API membership behavior.

## Decisions

1. **Keep the family sources separate.** Add `CqrsMessagingAttributes.cs` and `WebApiAttributes.cs` under the existing `contentFiles/cs/any` source set, matching the separation used for layered/clean and DDD definitions. This keeps ownership and review boundaries clear while using the existing `RoleAnnotationAttributeBase`.

2. **Treat the v1 manifest as the only role and target authority.** Each definition will be internal, sealed, have a public parameterless constructor, inherit the shared six-property base, and declare the exact class/struct/interface targets in the manifest. Extend the manifest-backed source tests with the two family counts and identities; do not duplicate the role catalog in production code or documentation.

3. **Do not change Core recognition.** The canonical resolver already maps all approved manifest roles by exact FQN. Add focused tests that apply each new source attribute to a catalog-identified fixture assembly and assert the existing role and metadata result. Add representative semantic integration fixtures for CQRS/API composition, custom-mapping parity, deterministic role conflicts, and the #525 API-membership boundary.

4. **Reuse the existing output and policy paths.** Exercise representative role facts through existing selector/context, coverage/explain, CLI, and Testing projections. No separate scanner, role index, selector model, or API marker is introduced. Existing #525 structural selection remains responsible for reviewed API membership.

Alternatives considered:

- Repeating six metadata properties in every annotation type was rejected because the shared base already defines that stable source contract.
- Extending Core mappings or adding YAML examples for the new types was rejected because the reviewed catalog and current resolver already provide exact-FQN recognition.
- A combined file for all role families was rejected because it would blur the established family-by-family source ownership.

## Risks / Trade-offs

- Hand-authored source identities or `AttributeUsage` declarations could drift from the manifest. → The source tests compare every definition against its manifest FQN, target, visibility, constructor, inheritance, multiplicity, and metadata properties.
- A new role could be mistaken for runtime framework behavior or an API-membership marker. → Tests and fixtures use the existing semantic role index and explicit #525 selectors; source attributes contain no framework dependencies or policy behavior.
