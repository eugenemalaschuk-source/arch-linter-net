using System.Reflection;
using System.Reflection.Emit;

namespace ArchLinterNet.Core.Tests;

// Builds a source type whose single field references a type from a second assembly that is deleted
// before the source assembly is loaded, so reflecting on the field's type throws
// FileNotFoundException - the same failure mode as a missing optional/transitive dependency in a real
// partially-loadable build output. Used to reproduce the incomplete-reflection-scan scenario from
// PR #306's review without needing a real broken package reference.
internal sealed class UnloadableFieldFixture : IDisposable
{
    private readonly string _tempDir;

    private UnloadableFieldFixture(string tempDir, Assembly consumerAssembly, Type sourceType)
    {
        _tempDir = tempDir;
        ConsumerAssembly = consumerAssembly;
        SourceType = sourceType;
    }

    public Assembly ConsumerAssembly { get; }

    public Type SourceType { get; }

    public static UnloadableFieldFixture Create(Action<TypeBuilder>? configureConsumerType = null)
    {
        string unique = Guid.NewGuid().ToString("N");
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-unloadable-{unique}");
        Directory.CreateDirectory(tempDir);

        string dependencyPath = Path.Combine(tempDir, "UnloadableDependency.dll");
        string consumerPath = Path.Combine(tempDir, "UnloadableConsumer.dll");

        var dependencyBuilder = new PersistedAssemblyBuilder(new AssemblyName($"UnloadableDependency_{unique}"), typeof(object).Assembly);
        ModuleBuilder dependencyModule = dependencyBuilder.DefineDynamicModule("UnloadableDependency.dll");
        TypeBuilder dependencyTypeBuilder = dependencyModule.DefineType("UnloadableTargetType", TypeAttributes.Public);
        Type dependencyType = dependencyTypeBuilder.CreateType();
        dependencyBuilder.Save(dependencyPath);

        var consumerBuilder = new PersistedAssemblyBuilder(new AssemblyName($"UnloadableConsumer_{unique}"), typeof(object).Assembly);
        ModuleBuilder consumerModule = consumerBuilder.DefineDynamicModule("UnloadableConsumer.dll");
        TypeBuilder consumerTypeBuilder = consumerModule.DefineType("SourceWithUnloadableFieldReference", TypeAttributes.Public);
        consumerTypeBuilder.DefineField("Target", dependencyType, FieldAttributes.Public);
        configureConsumerType?.Invoke(consumerTypeBuilder);
        consumerTypeBuilder.CreateType();
        consumerBuilder.Save(consumerPath);

        File.Delete(dependencyPath);

        Assembly loaded = Assembly.LoadFrom(consumerPath);
        Type sourceType = loaded.GetType("SourceWithUnloadableFieldReference")!;

        return new UnloadableFieldFixture(tempDir, loaded, sourceType);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch (IOException)
        {
            // The consumer assembly file may still be memory-mapped by the loader; best-effort cleanup only.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}
