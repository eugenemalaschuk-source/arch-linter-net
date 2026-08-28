## 1. Collection-integrity contract

- [x] 1.1 Separate valid produced-record state from the joined collection-integrity outcome; verify the modified specification covers a missing `not_applicable` record without permitting an invalid membership/state pair.
- [x] 1.2 Require produced-to-expected anti-join validation before the canonical left join; verify an orphan identity makes the collection visibly unassessable.

## 2. Native evidence shape

- [x] 2.1 Constrain evidence dimensions to the sparse, typed dimensions meaningful to the family and configured control; verify topology and external SARIF may omit one another’s irrelevant dimensions.

## 3. Validation and completion

- [x] 3.1 Review proposal, design, and delta specification for #505 scope discipline; verify they add no runtime implementation, policy schema, public API, CLI behavior, or counting engine.
- [x] 3.2 Run strict change validation and full OpenSpec validation after archive; verify the archived main specification passes both validations.
