using NUnit.Framework;

namespace ArchLinterNet.Unity.Tests;

[TestFixture]
public class AsmdefValidatorTests
{
    [Test]
    public void Validate_ShouldReturnTrue()
    {
        var validator = new AsmdefValidator();
        var result = validator.Validate("dummy.asmdef");
        Assert.That(result, Is.True);
    }
}
