## ADDED Requirements

### Requirement: Budget governance facts remain available to static policy consumers
The effective policy context SHALL project every metric-budget contract with its
declared metric identity and each configured absolute `minimum` and `maximum`
bound. Static policy consumers SHALL use those typed facts without triggering
metric evaluation or architecture analysis.

#### Scenario: A context exports a one-sided metric budget
- **WHEN** a policy declares a strict metric budget with `metric: components`
  and `maximum: 10`
- **THEN** its effective policy context contains typed `metric` and `maximum`
  facts for that budget and does not report an unsupported contract type
