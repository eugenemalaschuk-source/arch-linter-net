using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Validation.Abstractions;

public interface IArchitecturePublicApiApplicationService
{
    PublicApiCaptureOutcome Capture(PublicApiCaptureRequest request);

    PublicApiDiffOutcome Diff(PublicApiDiffRequest request);

    PublicApiUpdateOutcome Update(PublicApiUpdateRequest request);

    PublicApiMigrateOutcome Migrate(PublicApiMigrateRequest request);
}
