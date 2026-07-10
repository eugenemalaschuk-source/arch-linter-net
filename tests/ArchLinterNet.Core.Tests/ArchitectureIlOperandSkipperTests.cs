using System.Reflection.Emit;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureIlOperandSkipperTests
{
    [TestCase(OperandType.InlineNone, 0)]
    [TestCase(OperandType.ShortInlineR, 4)]
    [TestCase(OperandType.InlineVar, 2)]
    [TestCase(OperandType.InlineI, 4)]
    [TestCase(OperandType.InlineR, 8)]
    public void TryReadMetadataTokenIfPresent_AdvancesFixedWidthOperands(OperandType operandType, int size)
    {
        OpCode opCode = typeof(OpCodes).GetFields()
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .First(code => code.OperandType == operandType);
        int position = 0;

        bool result = ArchitectureIlOperandSkipper.TryReadMetadataTokenIfPresent(
            opCode, new byte[16], ref position, out int token);

        Assert.That(result, Is.True);
        Assert.That(token, Is.Zero);
        Assert.That(position, Is.EqualTo(size));
    }

    [Test]
    public void TryReadMetadataTokenIfPresent_HandlesTokenAndSwitchOperands()
    {
        int tokenPosition = 0;
        bool tokenResult = ArchitectureIlOperandSkipper.TryReadMetadataTokenIfPresent(
            OpCodes.Call, new byte[] { 1, 2, 3, 4 }, ref tokenPosition, out int token);
        Assert.That(tokenResult, Is.True);
        Assert.That(token, Is.EqualTo(0x04030201));
        Assert.That(tokenPosition, Is.EqualTo(4));

        int switchPosition = 0;
        bool switchResult = ArchitectureIlOperandSkipper.TryReadMetadataTokenIfPresent(
            OpCodes.Switch, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            ref switchPosition, out token);
        Assert.That(switchResult, Is.True);
        Assert.That(switchPosition, Is.EqualTo(12));
    }

    [Test]
    public void TryReadMetadataTokenIfPresent_TruncatedOperandsReturnFalse()
    {
        int position = 0;
        Assert.That(ArchitectureIlOperandSkipper.TryReadMetadataTokenIfPresent(
            OpCodes.Call, new byte[3], ref position, out _), Is.False);

        position = 0;
        Assert.That(ArchitectureIlOperandSkipper.TryReadMetadataTokenIfPresent(
            OpCodes.Switch, new byte[] { 1, 0, 0 }, ref position, out _), Is.False);

        position = 0;
        Assert.That(ArchitectureIlOperandSkipper.TryReadMetadataTokenIfPresent(
            OpCodes.Ldc_I8, new byte[7], ref position, out _), Is.False);
    }
}
