using System.Text;
using ArchLinterNet.Cli.Commands.Badge.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class ArchitectureHealthBadgeDisclosureValidatorTests
{
    private const string Headline =
        "{\"schemaVersion\":1,\"label\":\"architecture\",\"message\":\"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules\",\"color\":\"brightgreen\"}";
    private const string Freshness =
        "{\"schemaVersion\":1,\"label\":\"architecture\",\"message\":\"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules\",\"color\":\"brightgreen\",\"verified_at\":\"2026-09-09T10:00:00Z\",\"valid_until\":\"2026-09-09T11:00:00Z\"}";

    [TestCase("headline-only/v1", Headline, "b6a3501a87dc39495210674cfabdb19478df2cd7c701a05a0ea3c397d2166e3d")]
    [TestCase(
        "headline-plus-freshness/v1",
        Freshness,
        "e4630743393380aadb96dd4ee8d2cffd43488ef4869a7ca66379db755485ed32")]
    public void Validator_AcceptsCanonicalBytesAndReturnsTheirDigest(string profile, string json, string expectedDigest)
    {
        bool valid = ArchitectureHealthBadgeDisclosureValidator.TryValidate(profile, Encoding.UTF8.GetBytes(json), out string digest);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(digest, Is.EqualTo(expectedDigest));
        });
    }

    [TestCase("{\"schemaVersion\":1,\"label\":\"architecture\",\"label\":\"other\",\"message\":\"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules\",\"color\":\"brightgreen\"}")]
    [TestCase("{\"schemaVersion\":1,\"label\":\"architecture\",\"message\":\"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules\",\"color\":\"brightgreen\",\"extra\":\"not-allowed\"}")]
    [TestCase("{\"schemaVersion\":1,\"label\":\"arch\\u0069tecture\",\"message\":\"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules\",\"color\":\"brightgreen\"}")]
    [TestCase("{\"schemaVersion\":1,\"label\":\"architecture\",\"message\":\"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules\",\"color\":\"red\"}")]
    [TestCase("{\"schemaVersion\":1,\"label\":\"architecture\",\"message\":\"free text\",\"color\":\"brightgreen\"}")]
    [TestCase(" {\"schemaVersion\":1,\"label\":\"architecture\",\"message\":\"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 42 rules\",\"color\":\"brightgreen\"}")]
    public void Validator_RejectsNoncanonicalOrOutOfProfileBytes(string json)
    {
        bool valid = ArchitectureHealthBadgeDisclosureValidator.TryValidate("headline-only/v1", Encoding.UTF8.GetBytes(json), out _);

        Assert.That(valid, Is.False);
    }

    [Test]
    public void Validator_RejectsOversizePayload()
    {
        byte[] oversize = Enumerable.Repeat((byte)' ', ArchitectureHealthBadgeDisclosureValidator.MaximumPayloadBytes + 1).ToArray();

        Assert.That(
            ArchitectureHealthBadgeDisclosureValidator.TryValidate("headline-only/v1", oversize, out _),
            Is.False);
    }
}
