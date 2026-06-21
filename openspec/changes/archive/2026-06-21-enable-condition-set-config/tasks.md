## 1. Model

- [x] 1.1 Add `ConditionSets` and `DefaultConditionSet` properties to `ArchitectureAnalysisConfiguration` in `ArchitectureContractModels.cs`
- [x] 1.2 Verify `IgnoreUnmatchedProperties` in the YAML deserializer ensures backward compatibility with existing policies

## 2. Source Scanner

- [x] 2.1 Add `IReadOnlyList<string>? preprocessorSymbols` parameter to `ArchitectureSourceScanner.FindMethodBodyViolations`
- [x] 2.2 Update `BuildCompilation` to create `CSharpParseOptions.WithPreprocessorSymbols(symbols)` when symbols are non-empty and pass to `CSharpSyntaxTree.ParseText`

## 3. Runner

- [x] 3.1 Add `IReadOnlyList<string>? preprocessorSymbols` parameter to `ArchitectureContractRunner` primary constructor and store as `_preprocessorSymbols`
- [x] 3.2 Pass `_preprocessorSymbols` to `ArchitectureSourceScanner.FindMethodBodyViolations` in `CheckMethodBodyContract`
- [x] 3.3 Verify `ArchitectureIlMethodBodyScanner.FindMethodBodyViolations` call in `CheckMethodBodyContract` does NOT receive symbols

## 4. CLI

- [x] 4.1 Add `--condition-set <name>` option parsing to `Program.cs`
- [x] 4.2 Implement resolution logic: CLI arg → `analysis.default_condition_set` → empty list
- [x] 4.3 Implement unknown condition set validation with exit code 2 and listing available sets
- [x] 4.4 Pass resolved symbols to `ArchitectureContractRunner` constructor
- [x] 4.5 Update `PrintHelp()` to document `--condition-set`

## 5. Testing Adapter

- [x] 5.1 Add `WithConditionSet(string name)` method to `ArchitectureValidationBuilder` that stores the name
- [x] 5.2 In `Validate(contracts)`, resolve condition set name to symbols and pass to runner constructor

## 6. Validator

- [x] 6.1 Add optional `IReadOnlyList<string>? preprocessorSymbols` parameter to `ArchitectureValidator.Validate`
- [x] 6.2 Pass symbols through to `ArchitectureContractRunner` constructor

## 7. Schema and Docs

- [x] 7.1 Update `architecture/dependencies.arch.schema.json` with `condition_sets` and `default_condition_set` fields under `analysis`
- [x] 7.2 Update reference docs / sample YAML to document `analysis.condition_sets` and `analysis.default_condition_set`
- [x] 7.3 Add a docs statement: "Condition sets affect Roslyn/source method-body scanning only. Reflection/IL scanners analyze the assemblies provided to the run."
- [x] 7.4 Update AI-facing guidance in `openspec/specs/ai-policy-authoring/spec.md` if applicable

## 8. Tests

- [x] 8.1 Add test: default condition set resolves to empty symbols when nothing configured
- [x] 8.2 Add test: runtime symbol set (empty) excludes `#if DEBUG` forbidden calls
- [x] 8.3 Add test: debug symbol set (`DEBUG`) includes `#if DEBUG` forbidden calls
- [x] 8.4 Add test: negation flips correctly — `#if !DEBUG` excluded when `DEBUG` is defined
- [x] 8.5 Add test: multiple symbols work correctly — `#if DEBUG` visible with `[UNITY_EDITOR, DEBUG]`
- [x] 8.6 Add test: unknown condition set name in CLI produces exit code 2
- [x] 8.7 Add test: policy with `analysis.condition_sets` and `default_condition_set` loads successfully
- [x] 8.8 Run `rtk make acceptance` to verify no regressions
