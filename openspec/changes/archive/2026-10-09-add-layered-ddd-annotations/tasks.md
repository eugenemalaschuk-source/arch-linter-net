## 1. Add the role source definitions

- [x] 1.1 Add the shared internal role-attribute base with the six optional metadata properties and verify it compiles when included by the Core test project.
- [x] 1.2 Add the nine layered/clean role attributes with manifest-matched FQNs and targets; verify the source-shape tests cover every role.
- [x] 1.3 Add the eight DDD role attributes with manifest-matched FQNs and type targets; verify the source-shape tests cover every role.

## 2. Prove source and classifier compatibility

- [x] 2.1 Link the annotation content source and v1 manifest into Core tests; add manifest-backed assertions for identity, target, accessibility, multiplicity, inheritance, constructor, and metadata properties, then run the focused tests.
- [x] 2.2 Add representative explicit-mapping tests showing the new attributes produce the expected role and metadata through the existing classifier/index path; run the focused test fixture.

## 3. Validate and archive

- [x] 3.1 Format changed C# files and inspect the diff; run the required focused Core tests and directly implicated OpenSpec checks.
- [x] 3.2 Synchronize the approved semantic annotation spec, archive this OpenSpec change, and verify `openspec validate --all` succeeds.
