# ArchLinterNet

## Purpose

ArchLinterNet is a reusable .NET architecture-governance engine that:

- loads architecture policy from YAML;
- resolves target assemblies for analysis;
- evaluates dependency contracts (strict/audit style);
- scans both type-level and method-body-level dependencies;
- emits structured violations and cycle diagnostics.

Primary design goals:

- Unity-independent runtime;
- CLI-first execution;
- policy-driven behavior (YAML, not hardcoded tests);
- reusable in non-Unity backends.

---

## Public API Surface

### Contracts and YAML

- `ArchitectureContractLoader`
- `ArchitectureContractDocument`
- `ArchitectureContractGroups`
- `ArchitectureDependencyContract`
- `ArchitectureLayerContract`
- `ArchitectureAllowOnlyContract`
- `ArchitectureCycleContract`
- `ArchitectureMethodBodyContract`
- `ArchitectureAsmdefContract`
- `ArchitectureIndependenceContract`
- `ArchitectureIgnoredViolation`
- `ArchitectureAnalysisConfiguration`

### Execution

- `ArchitectureAnalysisContext`
- `ArchitectureAssemblyResolver`
- `ArchitectureContractRunner`

### Reporting and Results

- `ArchitectureViolation`
- `ArchitectureDiagnosticFormatter`

### Utilities

- `ArchitectureRepositoryRootLocator`

---

## Execution Model

1. Load YAML into `ArchitectureContractDocument`.
2. Resolve target assemblies from `analysis.target_assemblies`.
   The resolver checks already loaded assemblies, then `Assembly.Load`, then optional probing paths from YAML.
3. Build `ArchitectureAnalysisContext` with:
   - repository root;
   - target assemblies.
4. Build `ArchitectureContractRunner`.
5. Enumerate strict/audit contract lists.
6. Execute each contract type and collect violations/cycles.
7. Render output for humans or CI artifacts.

---

## YAML Requirements

The engine expects `architecture/dependencies.arch.yml` with these top-level blocks:

- `version`
- `name`
- `layers`
- `legacy_runtime_layers`
- `analysis`
- `contracts`

`analysis.target_assemblies` is required for reflection + IL scanning.
`analysis.assembly_search_paths` is optional and is recommended for standalone CLI hosts.
You can also provide probe paths through `ARCHITECTURE_ASSEMBLY_SEARCH_PATHS` (path-separator delimited).

Example:

```yaml
version: 1
name: Example Architecture Contract

layers:
  app:
    namespace: MyCompany.App
  domain:
    namespace: MyCompany.Domain

legacy_runtime_layers: []

analysis:
  target_assemblies:
    - MyCompany.App
    - MyCompany.Domain
  assembly_search_paths: []

contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []

  audit: []
  audit_layers: []
  audit_allow_only: []
  audit_cycles: []
  audit_method_body: []
  audit_asmdef: []
  audit_independence: []
```

---

## Minimal Usage Example

```csharp
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Reporting;

string repositoryRoot = @"/path/to/repo";
ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromRepositoryRoot(repositoryRoot);

IReadOnlyCollection<System.Reflection.Assembly> targetAssemblies =
    ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);

ArchitectureAnalysisContext context = new(repositoryRoot, targetAssemblies);
ArchitectureContractRunner runner = new(context, document);

foreach (ArchitectureDependencyContract contract in runner.StrictContracts())
{
    List<ArchitectureViolation> violations = runner.CheckContract(contract);

    if (violations.Count == 0)
        continue;

    string human = ArchitectureDiagnosticFormatter.FormatViolationsForHumans(violations);
    string ciJson = ArchitectureDiagnosticFormatter.FormatViolationsForCiArtifacts(contract.Name, violations);

    System.Console.WriteLine(human);
    System.Console.WriteLine(ciJson);
}
```

---

## Contract Types and Behavior

### 1. Dependency (`strict` / `audit`)

Checks that source layer types do not reference forbidden layer namespaces.

### 2. Layer Order (`strict_layers` / `audit_layers`)

Import-linter style inward-only layering constraints.

### 3. Allow-Only (`strict_allow_only` / `audit_allow_only`)

Whitelist model: source may reference only explicitly allowed layers.

### 4. Cycle (`strict_cycles` / `audit_cycles`)

Detects directed cycles among selected layers.

### 5. Method-Body (`strict_method_body` / `audit_method_body`)

Detects forbidden calls in executable bodies using:

- Roslyn semantic symbol resolution from source;
- IL token fallback scanning from compiled assemblies.

This combination reduces false negatives when semantic resolution is incomplete.
The runner also merges duplicate semantic/IL matches by normalized descriptor to reduce duplicate findings.

### 6. asmdef (`strict_asmdef` / `audit_asmdef`)

Validates assembly definition dependency boundaries (Unity-focused but YAML-driven).

### 7. Independence (`strict_independence` / `audit_independence`)

Mutual separation across a set of layers (no cross references in either direction).

---

## Violation and Cycle Outputs

### Violations

`ArchitectureViolation` fields:

- `ContractName`
- `SourceType`
- `ForbiddenNamespace`
- `ForbiddenReferences`

### Cycles

Cycle checks return string paths.

### Formatter helpers

- `FormatViolationsForHumans(...)`
- `FormatCyclesForHumans(...)`
- `FormatViolationsForCiArtifacts(...)`
- `FormatCyclesForCiArtifacts(...)`

CI artifact payloads are JSON strings designed for machine parsing.

---

## Integration Pattern for Test Projects

Recommended pattern:

1. Keep reusable logic in ArchLinterNet.Core.
2. Keep test project as a thin runner adapter.
3. Test layer responsibilities:
   - load policy;
   - initialize target assemblies for that solution;
   - execute strict/audit contract sets;
   - fail test on non-empty result sets.

---

## Using in Backend Repositories

For backend repos (ASP.NET, workers, services):

1. Add package reference to ArchLinterNet.Core.
2. Add `architecture/dependencies.arch.yml` in repo root.
3. Define `analysis.target_assemblies` with backend assembly names.
4. Create a small test/CLI host that:
   - loads contract;
   - resolves assemblies;
   - executes strict/audit policies.

Backend-specific tips:

- Keep layer namespaces stable; policy keys can remain short.
- Start with strict contracts for high-confidence boundaries.
- Use audit contracts for migration/debt visibility.
- Keep `ignored_violations` narrow and issue-linked.
