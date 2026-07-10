using System.Reflection.Emit;

namespace ArchLinterNet.Core.Scanning;

// Shared IL-operand-skipping logic used by scanners that walk raw method-body IL bytes
// (ArchitectureExternalDependencyIlScanner, ArchitectureIlMethodBodyScanner) to advance past an
// opcode's operand, reading a metadata token when the operand is a token reference.
internal static class ArchitectureIlOperandSkipper
{
    public static bool TryReadMetadataTokenIfPresent(OpCode opCode, byte[] il, ref int position, out int token)
    {
        token = 0;

        switch (opCode.OperandType)
        {
            case OperandType.InlineMethod:
            case OperandType.InlineField:
            case OperandType.InlineType:
            case OperandType.InlineTok:
                return TryReadToken(il, ref position, out token);

            case OperandType.InlineSwitch:
                return TryAdvancePastInlineSwitch(il, ref position);

            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                return TryAdvance(il, ref position, 1);

            case OperandType.ShortInlineR:
                return TryAdvance(il, ref position, 4);

            case OperandType.InlineVar:
                return TryAdvance(il, ref position, 2);

            case OperandType.InlineI:
            case OperandType.InlineBrTarget:
            case OperandType.InlineSig:
            case OperandType.InlineString:
                return TryAdvance(il, ref position, 4);

            case OperandType.InlineR:
            case OperandType.InlineI8:
                return TryAdvance(il, ref position, 8);

            case OperandType.InlineNone:
                return true;

            default:
                return true;
        }
    }

    private static bool TryReadToken(byte[] il, ref int position, out int token)
    {
        token = 0;

        if (!CanRead(il, position, 4))
        {
            return false;
        }

        token = BitConverter.ToInt32(il, position);
        position += 4;
        return true;
    }

    private static bool TryAdvancePastInlineSwitch(byte[] il, ref int position)
    {
        if (!CanRead(il, position, 4))
        {
            return false;
        }

        int caseCount = BitConverter.ToInt32(il, position);
        int size = 4 + caseCount * 4;
        return TryAdvance(il, ref position, size);
    }

    private static bool TryAdvance(byte[] il, ref int position, int size)
    {
        if (!CanRead(il, position, size))
        {
            return false;
        }

        position += size;
        return true;
    }

    private static bool CanRead(byte[] il, int position, int size)
    {
        return size >= 0 && position >= 0 && position <= il.Length - size;
    }
}
