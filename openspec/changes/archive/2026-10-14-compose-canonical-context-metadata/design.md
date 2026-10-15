## Context

The semantic annotation manifest and catalog documentation already approve six metadata-only context identities. `CanonicalAnnotationCatalog` recognizes those identities as reserved and known, but `CanonicalAnnotationRoleResolver` currently resolves only role attributes, so context attributes do not enter `ArchitectureRoleIndex`. Role attributes already support the six bounded string properties, and the existing classification result, role index, selectors, and output projections already carry arbitrary metadata dictionaries.

The implementation therefore needs to compose a second kind of canonical evidence into the existing role result without creating another classification engine or changing the public role/index model.

## Goals / Non-Goals

**Goals:**

- Compile the six manifest-defined context-only attributes from the optional source package for type and assembly targets.
- Extract exact compile-time string values using `CustomAttributeData`, with safe handling of malformed metadata and unsupported package identity.
- Enrich the winning role's existing metadata using the reviewed precedence and deterministic conflict behavior.
- Preserve role-only policy behavior, custom YAML mappings, single-role classification, and existing selector/contract/projection paths.

**Non-Goals:**

- Adding metadata keys, roles, arbitrary tags, or runtime discovery.
- Changing role conflict semantics, public Core APIs, reviewed API membership, or policy authority.
- Adding another metadata/index model or changing package/runtime distribution.

## Decisions

1. **Use the manifest's existing context entries as the source of truth.** Add the six exact FQNs to the source-only annotation set with the manifest's constructor, targets, `AllowMultiple`, and inheritance contract. Do not add new identities or alter the manifest's reviewed role catalog.
2. **Keep context evidence on the existing internal classification path.** Extend the per-source candidate/resolver data to retain context-only values and diagnostics. `ArchitectureAttributeRoleExtractor` remains the authority for source enablement and role precedence; `ArchitectureRoleIndex` remains the consumer-facing descriptor.
3. **Validate canonical package identity before applying context evidence.** An assembly carrying a canonical role or context identity must have exactly one supported catalog marker. Invalid or missing identity suppresses canonical role and context evidence for that assembly and uses the existing canonical diagnostic projection.
4. **Compose metadata in the manifest-approved order.** Start with the winning role evidence metadata (whether it came from a canonical role attribute or a custom YAML mapping). Fill absent keys from type-scope context attributes, then fill remaining absent keys from assembly-scope context attributes. Equal values coalesce. Context attributes never create a role or replace role metadata.
5. **Resolve context conflicts independently from role conflicts.** At one context scope, equivalent values coalesce. If multiple evidence instances provide different values for one key, omit that key from that scope and emit a deterministic canonical diagnostic with stable evidence/value ordering; retain any otherwise valid role and unrelated metadata. A higher-precedence role value remains authoritative, and type context takes precedence over assembly context for keys not supplied by role evidence.
6. **Reuse existing projections.** Record invalid values as metadata failures and context conflicts as canonical annotation diagnostics. The existing role index, selectors, contextual contracts, coverage, explain, Human/JSON/SARIF, and Testing paths consume the composed descriptor without a second context model or new public output contract.

## Risks / Trade-offs

- **Risk:** Adding context facts to candidate combination could accidentally change role selection or disabled-source behavior. → Keep context data separate from the candidate's `Role`, use the existing type/assembly source-enable checks, and add tests for context-only attributes, legacy policies, and role conflicts.
- **Risk:** Metadata enumeration order could choose a value nondeterministically. → Group candidates by key and scope, compare all distinct ordinal string values, and sort diagnostic evidence and values before formatting.
- **Risk:** Context values could leak from an assembly role that loses to a type role. → Merge only the winning role's metadata; merge assembly context defaults separately under the approved context rule.

## Migration Plan

The package is optional and additive. Existing projects and role-only policies require no migration. Consumers that author one of the six context attributes opt into that evidence and must use a supported annotation package identity; custom user-owned attributes and explicit YAML mappings remain available.

## Open Questions

None. Issue #566 and the reviewed semantic annotation manifest settle the identity, metadata vocabulary, and merge contract.
