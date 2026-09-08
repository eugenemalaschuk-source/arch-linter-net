using System.Reflection;
using ArchLinterNet.Cli.Commands.Validate.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class ValidateCommandHandlerProfileMeasurementTests
{
    [Test]
    public void Constructor_PassesItsPreCompositionAllocationBaselineToTheProfileWriter()
    {
        ValidateCommandHandler handler = new(null!, null!, null!);

        long handlerBaseline = ReadField<long>(handler, "_allocatedBytesAtStart");
        object profileWriter = ReadField<object>(handler, "_profile");
        long profileWriterBaseline = ReadField<long>(profileWriter, "_allocatedBytesAtStart");

        Assert.That(profileWriterBaseline, Is.EqualTo(handlerBaseline));
    }

    private static T ReadField<T>(object instance, string fieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {instance.GetType().Name}.");
        return (T)field!.GetValue(instance)!;
    }
}
