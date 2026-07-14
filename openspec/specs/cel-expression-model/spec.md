# cel-expression-model Specification

## Purpose
Define the CEL (Common Expressions Language) expression model for ArchLinterNet policy predicates: the allowed YAML locations for the `when` field, two typed input contexts (source-position and target-position), the policy-load validation contract, safe function exposure constraints, and backward-compatibility rules. CEL `when` expressions are additive predicates AND-combined with existing `role` and `metadata` selector constraints. Coverage contracts, graph traversal, path-based facts, and external lookups are explicitly out of scope for this wave. The `when` field is not added to the live JSON schema until issue #163 implements runtime evaluation.
## Requirements
### Requirement: `when` is the only CEL expression field; all other string fields remain literal
Every YAML field not explicitly listed as a CEL location SHALL be treated as a literal string and MUST NOT be evaluated as a CEL expression. The `when` field is the sole CEL surface in the policy schema. Fields that are explicitly forbidden from CEL evaluation include: `reason`, `name`, `id`, `namespace`, `namespace_suffix`, `role`, `attribute`, metadata extraction values (`constructor[N]`, `property:Name`, `const:Full.Type.NAME`, literal scalars), `analysis.*`, `packages`, `external_dependencies`, `legacy_runtime_layers`, `ignored_violations` entries, and every field in `classification.*`.

#### Scenario: `reason` field is never evaluated as CEL
- **WHEN** a contract entry declares `reason: "type.namespace.startsWith('X')"`
- **THEN** the policy loader SHALL treat this as a literal string and SHALL NOT evaluate it as a CEL expression

#### Scenario: `role` field is never evaluated as CEL
- **WHEN** a selector declares `role: "type.role == 'DomainLayer'"`
- **THEN** the policy loader SHALL treat the value as a literal role name string and SHALL NOT evaluate it as a CEL expression

#### Scenario: `namespace` field is never evaluated as CEL
- **WHEN** a layer declares `namespace: "type.namespace.startsWith('X')"`
- **THEN** the policy loader SHALL treat this as a literal namespace prefix and SHALL NOT evaluate it as a CEL expression

### Requirement: `when` is allowed in semantic layer selectors
A `layers.<name>.selector` object SHALL support an optional `when` field containing a CEL expression string. The `when` predicate is AND-combined with the selector's `role` and `metadata` constraints: a type matches the selector only when its resolved role matches `role`, all `metadata` constraints pass, and the `when` expression evaluates to `true`. Omitting `when` is equivalent to no additional predicate and preserves existing selector behavior unchanged.

#### Scenario: Layer selector with `when` matches only types satisfying all three conditions
- **WHEN** a `layers.<name>.selector` declares `role: DomainLayer`, `metadata: { module: Sales }`, and `when: "type.namespace.startsWith('MyCompany')"`
- **THEN** only types whose resolved role is `DomainLayer`, whose resolved `module` metadata equals `Sales`, and whose namespace starts with `MyCompany` SHALL match the selector

#### Scenario: Layer selector without `when` is unaffected
- **WHEN** a `layers.<name>.selector` declares only `role` and `metadata` without `when`
- **THEN** its matching behavior is identical to existing selector behavior with no CEL evaluation

#### Scenario: Layer selector `when` evaluating to `false` excludes the type
- **WHEN** a layer selector's `when` expression evaluates to `false` for a candidate type
- **THEN** the type SHALL NOT match the selector, even if `role` and `metadata` both match

### Requirement: `when` is allowed on contextual contract selectors
The `source`, `forbidden`, `allowed`, and `exclude` selector objects within `strict_context_dependencies`, `audit_context_dependencies`, `strict_context_allow_only`, and `audit_context_allow_only` contract entries SHALL each support an optional `when` field. The `when` predicate is AND-combined with the selector's `role` and `metadata` constraints. The same additive semantics apply: a type matches only when all declared constraints pass.

#### Scenario: Source selector `when` filters contextual source candidates
- **WHEN** a `strict_context_dependencies` contract's `source` selector declares `when: "type.metadata.containsKey('module')"`
- **THEN** only types that both match the source `role`/`metadata` constraints AND have a `module` key in their resolved metadata SHALL be treated as contract source types

#### Scenario: Forbidden selector `when` restricts forbidden-match candidates
- **WHEN** a `forbidden` selector in a `strict_context_dependencies` contract declares `when: "type.metadata['module'] != source.metadata['module']"`
- **THEN** only target types whose `module` metadata differs from the matched source type's `module` metadata SHALL be treated as matching the forbidden selector

#### Scenario: Allowed selector `when` restricts allowed-target candidates
- **WHEN** an `allowed` selector in a `strict_context_allow_only` contract declares `when: "type.metadata['module'] == source.metadata['module']"`
- **THEN** only target types whose `module` metadata equals the source type's `module` metadata SHALL be counted as matching the allowed selector

### Requirement: Source-position `when` context contains only the candidate type facts
A `when` expression on a `source` selector or on a `layers.<name>.selector` SHALL have access to exactly these variables: `type.name` (string, fully-qualified type name), `type.namespace` (string, declared namespace), `type.assembly` (string, assembly simple name), `type.role` (string, resolved role or empty string if unclassified), and `type.metadata` (map from string to dyn, resolved metadata key/value pairs). No `source.*` variable SHALL be available in source-position `when` expressions.

#### Scenario: Source-position `when` accessing `type.namespace`
- **WHEN** a `source` selector declares `when: "type.namespace.startsWith('MyCompany.Product')"`
- **THEN** the expression SHALL be evaluated with `type.namespace` bound to the candidate type's declared namespace

#### Scenario: Source-position `when` referencing `source.*` fails at load time
- **WHEN** a `source` selector or layer selector's `when` expression references `source.role` or `source.metadata`
- **THEN** the policy loader SHALL reject the policy with a load-time configuration diagnostic indicating that `source` is not available in this context

### Requirement: Target-position `when` context adds matched-source facts
A `when` expression on a `forbidden`, `allowed`, or `exclude` selector SHALL have access to all source-position variables for the candidate target type (`type.*`) PLUS the matched source type's facts: `source.name` (string), `source.namespace` (string), `source.assembly` (string), `source.role` (string), and `source.metadata` (map from string to dyn). The `source.*` variables are bound to the specific source type that matched the contract's `source` selector for the current evaluation pair.

#### Scenario: Target-position `when` can compare target and source metadata
- **WHEN** a `forbidden` selector declares `when: "type.metadata['module'] != source.metadata['module']"`
- **THEN** the expression SHALL be evaluated with `type.*` bound to the candidate target type and `source.*` bound to the matched source type for that evaluation pair

#### Scenario: Target-position `when` accessing `source.assembly`
- **WHEN** an `allowed` selector declares `when: "type.assembly == source.assembly"`
- **THEN** the expression SHALL be evaluated with `source.assembly` bound to the matched source type's assembly simple name

### Requirement: `when` expression MUST return bool; non-boolean result is a load-time error where statically inferrable
A `when` expression SHALL produce a `bool` result. Where the CEL type-checker can statically determine the return type is not `bool`, the policy loader SHALL reject the expression with a load-time configuration diagnostic.

#### Scenario: Expression returning string fails at load time when statically inferrable
- **WHEN** a `when` field contains `"type.namespace"` (evaluates to string, not bool)
- **AND** the CEL type-checker can determine this statically
- **THEN** the policy loader SHALL reject the policy with a load-time diagnostic citing a non-boolean return type

#### Scenario: Expression involving `dyn` map access may only fail at evaluation time
- **WHEN** a `when` field contains `"type.metadata['key'] > 5"` and `type.metadata['key']` is typed as `dyn`
- **THEN** the policy loader is NOT required to reject this at load time; a type failure for this expression may only manifest at evaluation time as a deterministic evaluation-time diagnostic

### Requirement: Parse errors and unknown identifiers fail at policy-load time
A `when` expression that fails CEL parsing (syntax error) or that references a variable name not declared in its input context SHALL cause the policy loader to reject the policy document with a load-time configuration diagnostic identifying the expression and the error.

#### Scenario: Syntax error in `when` expression fails at load time
- **WHEN** a `when` field contains `"type.role == "` (incomplete expression, parse error)
- **THEN** the policy loader SHALL reject the policy with a load-time diagnostic citing the parse error location and message

#### Scenario: Unknown variable reference fails at load time
- **WHEN** a `when` expression references an identifier not in the declared context (e.g., `assembly.references`)
- **THEN** the policy loader SHALL reject the policy with a load-time diagnostic identifying the unknown identifier

### Requirement: Unsupported function calls fail at policy-load time
A `when` expression that calls a function not in the approved function whitelist SHALL cause the policy loader to reject the policy with a load-time configuration diagnostic. The approved whitelist is determined by #166 and #163; this requirement defines the failure mode.

#### Scenario: Call to a disallowed function fails at load time
- **WHEN** a `when` expression calls a function outside the approved whitelist
- **THEN** the policy loader SHALL reject the policy with a load-time diagnostic identifying the unsupported function name

#### Scenario: Host-access function (file, network, process, reflection) is never whitelisted
- **WHEN** a `when` expression attempts to call any function that accesses the file system, network, OS processes, CLR reflection APIs, or any runtime plugin API
- **THEN** the policy loader SHALL reject the expression; no such function SHALL be in the approved whitelist

### Requirement: Existing policies without `when` remain fully backward compatible
A policy that does not declare `when` on any selector SHALL behave identically after the `when` field is added to the JSON schema by #163. No existing policy file requires modification. The `when` field is optional with absence equivalent to an always-true predicate.

#### Scenario: Existing layer selector without `when` is unaffected
- **WHEN** an existing policy declares `layers.<name>.selector` with only `role` and `metadata`
- **THEN** the behavior after #163 ships the `when` field is identical to the behavior before

#### Scenario: Existing contextual contract without `when` is unaffected
- **WHEN** an existing `strict_context_dependencies` contract entry's selectors do not declare `when`
- **THEN** the behavior after #163 ships the `when` field is identical to the behavior before

### Requirement: `when` must not be added to the live JSON schema until #163 implements evaluation
The production `schema/dependencies.arch.schema.json` SHALL NOT accept a `when` field on any selector shape until the runtime evaluation engine (issue #163) ships. Adding `when` to the schema before evaluation is implemented would allow policy authors to write expressions that are silently accepted and silently ignored — a form of schema deception. Until #163 ships, any `when` field in a policy YAML document SHALL be rejected by schema validation as `additionalProperties: false` already enforces for unknown fields.

#### Scenario: Policy with `when` field is rejected by current schema
- **WHEN** a policy YAML declares `layers.<name>.selector.when: "type.role == 'X'"`
- **AND** the live JSON schema has not yet been updated by #163
- **THEN** schema validation SHALL reject the document with an `additionalProperties` error

#### Scenario: `when` field is not silently ignored
- **WHEN** the schema eventually adds `when` (as part of #163)
- **THEN** the runtime SHALL evaluate it; a `when` field present in a policy SHALL NEVER be silently accepted and discarded without evaluation

### Requirement: CEL expressions must not access host services or external resources
A CEL expression evaluated by ArchLinterNet SHALL NOT have access to file system APIs, network APIs, OS process APIs, CLR reflection-over-live-assembly APIs, or runtime plugin APIs. The expression context is limited to the typed input variables declared for its selector position. This constraint is enforced by the function whitelist (#166) and the input context binding (#163) — no escape hatch through function registration, type-checking bypass, or dynamic evaluation is permitted.

#### Scenario: No file system access from `when` expression
- **WHEN** a `when` expression is evaluated for any type
- **THEN** the expression SHALL NOT be able to read, write, or enumerate any file or directory on the host

#### Scenario: No network access from `when` expression
- **WHEN** a `when` expression is evaluated for any type
- **THEN** the expression SHALL NOT be able to open sockets, make HTTP requests, or perform any network I/O

### Requirement: CEL expressions interact with coverage stale-selector detection consistently
A `layers.<name>.selector` that declares `when` SHALL produce a stale-selector fact when the combined `role`/`metadata`/`when` predicate matches zero types across the entire scanned type set. The `when` predicate MUST NOT exempt a selector from stale-selector detection; stale-selector behavior is consistent with selectors that do not declare `when`.

#### Scenario: Selector with `when` matching zero types produces stale-selector fact
- **WHEN** a layer selector declares `role: DomainLayer` and `when: "type.metadata['version'] == 99"` and no scanned type satisfies both
- **THEN** the model records a stale-selector fact for that layer, identical to the existing stale-selector behavior for selectors without `when`

#### Scenario: Coverage contracts do not gain `when` in this wave
- **WHEN** a `strict_coverage` or `audit_coverage` contract entry is declared
- **THEN** no `when` field is available on that entry; CEL expressions are not evaluated as part of coverage contract processing in this wave

