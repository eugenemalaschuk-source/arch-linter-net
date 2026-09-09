## 1. Decision record and release boundaries

- [x] 1.1 Add the internal Badge Relay ADR with the selected mode matrix, trust/disclosure boundaries, lifecycle state machine, numerical lease/cadence/cost bounds, and primary-source evidence; verify internal-doc lint and Markdown formatting.
- [x] 1.2 Record the authoritative child ownership, dependency DAG, compatibility/release handoff, and explicit non-goals; verify the ADR leaves no critical security, privacy, freshness, hosting, or credential TBD.

## 2. Executable protocol fixtures

- [x] 2.1 Add versioned JSON Schemas and a synthetic sample configuration for profiles, registry pins, validity bounds, and release compatibility; verify each JSON file parses and valid samples validate against its schema.
- [x] 2.2 Add canonical ready/unavailable data plus disclosure, OIDC, replay/context, stale-writer, expiry, revoke/restore, and cache adversarial vectors; verify vectors contain no real private adopter identity and cover the required positive and negative cases.

## 3. Integration evidence

- [x] 3.1 Synchronize OpenSpec artifacts with the actual ADR and fixtures, then run strict OpenSpec validation.
- [x] 3.2 Review the final diff for only issue #826 scope, run focused schema/vector and documentation checks plus `make fmt`, and archive the OpenSpec change; verify the generated main spec has a Purpose and Requirements section.
- [x] 3.3 Synchronize #825, #827, and #828 with final contract names/defaults/ownership and retain their blocked status pending ADR review; verify the issue graph still hands release distribution to #835 and released-artifact verification to #806.
