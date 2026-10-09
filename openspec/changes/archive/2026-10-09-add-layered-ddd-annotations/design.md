## Context

See `proposal.md` for motivation and `specs/semantic-annotation-contract/spec.md` for the observable contract. The v1 catalog manifest already fixes the 17 role identities, targets, and metadata properties. #567 owns NuGet packing and #568 owns built-in classification recognition.

## Goals / Non-Goals

**Goals:**

- Add the #569 role definitions to the package's canonical content-files source set.
- Keep the source metadata aligned with the manifest and usable through the existing attribute extraction model.

**Non-Goals:**

- Implementing package build and validation mechanics, canonical recognition, context-only annotations, or other role families.
- Adding annotation behavior that grants policy permissions or changes public API membership.

## Decisions

- Place the definitions under `src/ArchLinterNet.Annotations/contentFiles/cs/any/`, matching the approved package content path.
- Put layered/clean and DDD declarations in separate source files so the parallel role-family work has independent ownership.
- Share the six named metadata properties through a small internal abstract `Attribute` base. Every concrete role type remains internal, sealed, and declares its own manifest-matched `AttributeUsage`.
- Link the source set into Core tests and compare reflected identities, targets, visibility, multiplicity, inheritance, constructor, and property shape against the manifest. Exercise representative attributes through the existing explicit-mapping classifier path; built-in FQN recognition remains with #568.

Alternatives considered:

- Repeating six property declarations in every role type would increase source duplication without changing the contract.
- Adding the package and Core mapping catalog here would overlap #567/#568 and couple this role-family issue to their separate integration work.

## Risks / Trade-offs

- [Risk] A shared base type may be mistaken for another canonical role identity. → Mitigation: it is abstract, internal, not used as an annotation, and role recognition remains exact-FQN based.
- [Risk] Hand-authored definitions can drift from the manifest. → Mitigation: manifest-backed reflection tests fail on identity or target drift.
