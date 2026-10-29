## ADDED Requirements

### Requirement: Scan-scope and suppression changes fail closed

The trusted Qodana publisher SHALL reject pull requests that change analysis visibility inputs or expand inspection suppression scope. Analysis visibility inputs include any root or nested `.gitignore` file and any file whose basename ends in `.DotSettings`, case-insensitively. Suppression scope expands when a patch adds a suppression directive or deletes a directive that restores inspection or compiler-warning analysis.

#### Scenario: Pull request changes a ReSharper settings file
- **WHEN** a pull request changes any `.DotSettings` file
- **THEN** the publisher reports the file as a trusted scan-policy change and fails the Qodana status

#### Scenario: Pull request changes a Git ignore file
- **WHEN** a pull request changes a root or nested `.gitignore` file
- **THEN** the publisher reports the file as a trusted scan-coverage change and fails the Qodana status

#### Scenario: Pull request deletes a suppression restoration directive
- **WHEN** a patch deletes a ReSharper, Qodana, or compiler-warning restore directive
- **THEN** the publisher reports that suppression scope may expand and fails the Qodana status
