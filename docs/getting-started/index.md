# Getting Started

This guide walks through adding ArchLinterNet to a .NET repository.

## 1. Create a policy file

Create `architecture/dependencies.arch.yml` at your repository root:

```yaml
version: 1
name: My Architecture Contract

layers:
  app:
    namespace: MyApp.Application
  domain:
    namespace: MyApp.Domain
  infrastructure:
    namespace: MyApp.Infrastructure

analysis:
  target_assemblies:
    - MyApp.Application
    - MyApp.Domain
    - MyApp.Infrastructure

contracts:
  strict:
    - name: app-must-not-depend-on-infrastructure
      source: app
      forbidden: [infrastructure]
      reason: Application layer should not depend on infrastructure directly.

  strict_layers:
    - name: clean-architecture-layering
      layers:
        - infrastructure
        - app
        - domain
      reason: Dependencies must point inward toward the domain.
```

## 2. Run validation

Using the .NET tool:

```bash
arch-linter-net
```

Or programmatically from a test:

```csharp
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Reporting;

string repositoryRoot = @"/path/to/repo";
string policyPath = Path.Combine(repositoryRoot, "architecture", "dependencies.arch.yml");

var document = ArchitectureContractLoader.LoadFromPath(policyPath);
var resolution = ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);

var context = new ArchitectureAnalysisContext(
    repositoryRoot,
    resolution.ResolvedAssemblies,
    resolution.MissingAssemblyNames,
    resolution.AssemblyProbingPaths);

var runner = new ArchitectureContractRunner(context, document);

foreach (var contract in runner.StrictContracts())
{
    var violations = runner.CheckContract(contract);
    if (violations.Count == 0) continue;

    string output = ArchitectureDiagnosticFormatter.FormatViolationsForHumans(violations);
    Console.WriteLine(output);
}
```

## 3. Interpret results

Violations show the contract name, source type, and forbidden references:

```
[VIOLATION] app-must-not-depend-on-infrastructure
  MyApp.Application.Services.MyService
    → MyApp.Infrastructure.Repositories.UserRepository
    → MyApp.Infrastructure.Data.DbContext

[VIOLATION] clean-architecture-layering
  MyApp.Infrastructure.Importer
    → MyApp.Domain.Abstractions.IUnitOfWork
```

## 4. Next steps

- Read the [Policy Format](../policy-format/index.md) guide for a complete reference
- Check [CI Integration](../guides/ci-integration.md) for adding validation to your pipeline
- Explore [Migration Baselines](../guides/migration-baselines.md) for managing existing violations
