using NUnit.Framework;

namespace ArchLinterNet.Unity.Tests;

[TestFixture]
public class AsmdefValidatorTests
{
    [Test]
    public void Validate_NonExistentFile_ThrowsFileNotFoundException()
    {
        var validator = new AsmdefValidator();

        Assert.Throws<System.IO.FileNotFoundException>(() =>
            validator.Validate("nonexistent.yml"));
    }
}
