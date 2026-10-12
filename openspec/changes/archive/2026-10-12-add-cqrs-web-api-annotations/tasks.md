## 1. Add the approved source definitions

- [x] 1.1 Add the 8 manifest-defined CQRS/messaging attributes in a separate content-file source and verify their FQNs, targets, visibility, constructors, and metadata properties with the source-contract test.
- [x] 1.2 Add the 6 manifest-defined web/API attributes in a separate content-file source and verify their FQNs, targets, visibility, constructors, and metadata properties with the source-contract test.

## 2. Prove canonical classification

- [x] 2.1 Extend the manifest-backed source contract coverage for `cqrs-messages` and `web-api`, then run the focused source-contract tests.
- [x] 2.2 Prove all 14 new attributes resolve through the existing exact-FQN classifier without YAML mappings, preserve bounded metadata, and match equivalent custom-mapping role/metadata results; verify through focused Core tests.
- [x] 2.3 Add representative CQRS/API role-index fixtures and deterministic same-type role-conflict coverage; verify command/handler, query/read-model, integration-event, DTO, controller, and endpoint facts.

## 3. Preserve semantic consumers and API membership

- [x] 3.1 Verify canonical role facts flow through the existing selector/contextual, coverage, and explain paths, with focused Core tests showing no runtime behavior is inferred.
- [x] 3.2 Extend the CLI role-report and Testing diagnostic fixtures with a canonical CQRS role FQN; verify their existing projections preserve available metadata and evidence provenance.
- [x] 3.3 Verify `ApiContract` evidence does not select reviewed API membership and a `ValueObject` selected through an authored #525 marker retains `ValueObject`; run the focused public-API selector test.

## 4. Complete the change lifecycle

- [x] 4.1 Run focused tests, formatter, relevant lint, and strict OpenSpec validation; inspect the final diff for scope and format.
- [x] 4.2 Synchronize the semantic annotation spec, archive `add-cqrs-web-api-annotations`, and verify `openspec validate --all` succeeds.
