namespace ArchLinterNet.Core.Tests.SourceFactFixtures;

public sealed class SingleTypeFixture { }

public sealed class FileTypeA { }

public sealed class FileTypeB { }

public interface IFixtureInterface { }

public struct FixtureStruct { }

public enum FixtureEnum { ValueA }

public record FixtureRecord(string Name);

public record struct FixtureRecordStruct(int X);

public sealed class OuterFixture
{
    public sealed class InnerFixture { }
}

public sealed class GenericFixture<T> { }

public sealed partial class PartialFixture { }
