## Why

Existing cycle contracts (`strict_cycles`/`audit_cycles`) require policy authors to explicitly list every layer in the cycle check. When an architecture rule is broader — "all feature siblings must be acyclic" — this becomes repetitive and fragile as new siblings are added without updating the contract. Acyclic sibling checks discover sibling namespaces automatically from a configured ancestor, reducing maintenance and catching cycles in newly added sibling modules without policy changes.

## What Changes

- Add a new contract family `acyclic_siblings` in both `strict` and `audit` variants
- Introduce `ArchitectureAcyclicSiblingContract` model with `ancestors` (list of namespace prefixes), inheriting common contract fields
- Implement namespace discovery: scan loaded types under each configured ancestor, group by immediate child namespace segment, attribute descendant dependencies to the sibling group
- Reuse `ArchitectureCycleDetector` for cycle detection on the sibling graph
- Report diagnostics with ancestor context: `"<ancestor>: <siblingA> -> <siblingB> -> <siblingC> -> <siblingA>"`
- Wire into `ArchitectureContractRunner`, `ArchitectureValidator`, CLI, and Testing adapters
- Update JSON schema, policy docs, and AI-facing guidance
- Add sample policy demonstrating acyclic sibling checks
- No breaking changes to existing contract types

## Capabilities

### New Capabilities
- `acyclic-sibling-contracts`: Auto-discovery of dependency cycles between direct sibling namespaces under configured ancestor namespaces

### Modified Capabilities
- (none — existing cycle contracts are unchanged)

## Impact

- **New types**: `ArchitectureAcyclicSiblingContract` model class, new checking method in runner
- **Modified types**: `ArchitectureContractGroups` (new list properties), `ArchitectureContractLoader` (fallback IDs, duplicate validation), `ArchitectureValidator` (new contract loop), `ArchitectureContractRunner` (accessors + checking)
- **CLI**: Enumerate and run new contracts in strict/audit mode
- **Testing**: New assertion surface in `ArchitectureAssertions`
- **Schema**: New `$defs/acyclicSiblingContract` and contract group properties under `contracts`
- **No dependency changes**
