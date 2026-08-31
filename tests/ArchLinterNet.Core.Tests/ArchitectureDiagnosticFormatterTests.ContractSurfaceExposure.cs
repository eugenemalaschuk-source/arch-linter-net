using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractSurfaceExposureReportingTests
{
    private static readonly int[] _expectedMatchingForbiddenSelectors = [1, 3];

    [Test]
    public void FormatViolationsForHumans_ContractSurfaceExposure_FormatsCompleteOptionalContext()
    {
        var formatter = new ArchitectureDiagnosticFormatter();
        string withOptionalContext = formatter.FormatViolationsForHumans([CreateExposureViolation(
            memberOrMetadataSite: "method:OrdersController.Get",
            reviewedPublicApiSurface: "reviewed-api")]);
        string withoutOptionalContext = formatter.FormatViolationsForHumans([CreateExposureViolation(
            memberOrMetadataSite: null,
            reviewedPublicApiSurface: null)]);
        string withEmptyOptionalContext = formatter.FormatViolationsForHumans([CreateExposureViolation(
            memberOrMetadataSite: string.Empty,
            reviewedPublicApiSurface: string.Empty,
            declaringSourceType: null)]);

        Assert.Multiple(() =>
        {
            Assert.That(withOptionalContext, Does.Contain("source_assembly: Product.Api"));
            Assert.That(withOptionalContext, Does.Contain("source_surface: exported"));
            Assert.That(withOptionalContext, Does.Contain("declaring_source_type: Product.Api.OrdersContract"));
            Assert.That(withOptionalContext, Does.Contain("exposure_path: method:Get.return"));
            Assert.That(withOptionalContext, Does.Contain("canonical_exposure_path: 10:method3:Get6:return"));
            Assert.That(withOptionalContext, Does.Contain("target_assembly: Product.Internal"));
            Assert.That(withOptionalContext, Does.Contain("target_type: Product.Internal.OrderEntity"));
            Assert.That(withOptionalContext, Does.Contain("site: method:OrdersController.Get"));
            Assert.That(withOptionalContext, Does.Contain("reviewed_public_api_surface: reviewed-api"));
            Assert.That(withoutOptionalContext, Does.Not.Contain("site:").And.Not.Contain("reviewed_public_api_surface:"));
            Assert.That(withEmptyOptionalContext,
                Does.Not.Contain("site:").And.Not.Contain("reviewed_public_api_surface:"));
            Assert.That(withEmptyOptionalContext,
                Does.Contain("declaring_source_type: Product.Api.OrdersContract"));
        });
    }

    [Test]
    public void FormatResultAsSarif_ContractSurfaceExposure_PreservesPathRichProperties()
    {
        string sarif = new ArchitectureSarifFormatter().FormatResultAsSarif(
            "strict",
            [
                CreateExposureViolation("property:Order", "reviewed-api", [1, 3]),
                CreateExposureViolation(null, null, null),
            ],
            Array.Empty<string>(),
            "1.2.3");

        using JsonDocument document = JsonDocument.Parse(sarif);
        JsonElement properties = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("properties");

        Assert.Multiple(() =>
        {
            Assert.That(properties.GetProperty("source_assembly").GetString(), Is.EqualTo("Product.Api"));
            Assert.That(properties.GetProperty("declaring_source_type").GetString(),
                Is.EqualTo("Product.Api.OrdersContract"));
            Assert.That(properties.GetProperty("exposure_path").GetString(), Is.EqualTo("method:Get.return"));
            Assert.That(properties.GetProperty("canonical_exposure_path").GetString(),
                Is.EqualTo("10:method3:Get6:return"));
            Assert.That(properties.GetProperty("target_assembly").GetString(), Is.EqualTo("Product.Internal"));
            Assert.That(properties.GetProperty("target_type").GetString(), Is.EqualTo("Product.Internal.OrderEntity"));
            Assert.That(properties.GetProperty("source_surface").GetString(), Is.EqualTo("exported"));
            Assert.That(properties.GetProperty("member_or_metadata_site").GetString(), Is.EqualTo("property:Order"));
            Assert.That(properties.GetProperty("reviewed_public_api_surface").GetString(), Is.EqualTo("reviewed-api"));
            Assert.That(properties.GetProperty("matching_forbidden_selectors").EnumerateArray()
                .Select(item => item.GetInt32()), Is.EqualTo(_expectedMatchingForbiddenSelectors));
            Assert.That(document.RootElement.GetProperty("runs")[0].GetProperty("results")[1]
                .GetProperty("properties").GetProperty("matching_forbidden_selectors").ValueKind,
                Is.EqualTo(JsonValueKind.Null));
        });
    }

    private static ArchitectureViolation CreateExposureViolation(
        string? memberOrMetadataSite,
        string? reviewedPublicApiSurface,
        IReadOnlyCollection<int>? matchingForbiddenSelectors = null,
        string? declaringSourceType = "Product.Api.OrdersContract") =>
        new("no-internal-contract-types", "no-internal-contract-types", "Product.Api.OrdersContract",
            "Product.Internal", ["Product.Internal.OrderEntity"])
        {
            Payload = new ContractSurfaceExposurePayload(
                "Product.Api", declaringSourceType!, "method:Get.return", "10:method3:Get6:return",
                "Product.Internal", "Product.Internal.OrderEntity", "exported", memberOrMetadataSite,
                reviewedPublicApiSurface, matchingForbiddenSelectors),
        };
}
