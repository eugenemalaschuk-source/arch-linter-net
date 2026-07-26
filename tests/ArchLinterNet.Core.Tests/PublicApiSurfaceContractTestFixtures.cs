#pragma warning disable CS0649 // Fields exist only so the public API surface scanner can discover them.
#pragma warning disable CS0067 // Event exists only so the public API surface scanner can discover it.

namespace PublicApiSurfaceContractTestFixtures
{
    public sealed class CleanDeclaredType
    {
        public CleanDeclaredType()
        {
        }

        public int Value { get; set; }

        public static void DoWork()
        {
        }
    }

    public sealed class AccidentalPublicType;

    public sealed class AccidentalMemberType
    {
        public AccidentalMemberType()
        {
        }

        public int UndeclaredField;

        public int UndeclaredProperty { get; set; }

        public static void UndeclaredMethod()
        {
        }

        public event EventHandler? UndeclaredEvent;
    }

    public sealed class ConstantHolder
    {
        public ConstantHolder()
        {
        }

        public const string DeclaredConst = "declared";

        public const string UndeclaredConst = "undeclared";
    }

    public class ProtectedMemberHolder
    {
        public ProtectedMemberHolder()
        {
        }

        protected int ProtectedField;

        protected static void ProtectedMethod()
        {
        }
    }

    public class NestedContainerPublic
    {
        public NestedContainerPublic()
        {
        }

        public class NestedPublicType
        {
            public int Value;
        }

        protected class NestedProtectedType
        {
            public int Value;
        }
    }

    internal class NestedContainerInternal
    {
        public class NestedPublicInsideInternal
        {
            public int Value;
        }
    }

    public sealed class GenericHolder<T>
    {
        public GenericHolder()
        {
        }

        public T Value = default!;

        public TResult Map<TResult>(T input)
        {
            return default!;
        }
    }

    public sealed class ArrayRankHolder
    {
        public ArrayRankHolder()
        {
        }

        public static void TakeVector(int[] values)
        {
        }

        public static void TakeMatrix(int[,] values)
        {
        }

        public static void TakeCube(int[,,] values)
        {
        }
    }

    public enum PublicColor
    {
        Red,
        Green,
        Blue
    }

    public class VisibilityHolder
    {
        public VisibilityHolder()
        {
        }

        public static void PublicMethod()
        {
        }

        protected static void ProtectedMethod()
        {
        }
    }

    public abstract class OpenAbstractType
    {
    }

    public static class StaticUtilityType
    {
        public static void Do()
        {
        }
    }

    public readonly struct ReadOnlyStructType
    {
        public readonly int Value;
    }

    public interface IConstrainedInterface<T>
        where T : class, new()
    {
    }

    public class ConstrainedGenericType<T>
        where T : struct
    {
    }

    public class VirtualMethodHolder
    {
        public virtual void DoWork()
        {
        }
    }

    public abstract class AbstractMethodHolder : VirtualMethodHolder
    {
        public abstract void DoOtherWork();
    }

    public class OverrideMethodHolder : VirtualMethodHolder
    {
        public override void DoWork()
        {
        }
    }

    public class SealedOverrideMethodHolder : VirtualMethodHolder
    {
        public sealed override void DoWork()
        {
        }
    }

    public static class ParameterModifierHolder
    {
        public static void TakeRef(ref int value)
        {
        }

        public static void TakeOut(out int value)
        {
            value = 0;
        }

        public static void TakeIn(in int value)
        {
        }

        public static void TakeParams(params int[] values)
        {
        }
    }

    public static class GenericMethodConstraintHolder
    {
        public static void Do<T>()
            where T : class, new()
        {
        }
    }

    public class PropertyVariantHolder
    {
        public static int StaticProperty { get; set; }

        public int InitOnlyProperty { get; init; }

        public int PublicGetProtectedSetProperty { get; protected set; }

        public int PublicGetProtectedInternalSetProperty { get; protected internal set; }
    }

    public class FieldVariantHolder
    {
        public FieldVariantHolder()
        {
            ReadOnlyField = 0;
        }

        public static int StaticField;

        public readonly int ReadOnlyField;
    }

    public class EventVariantHolder
    {
        public static event EventHandler? StaticEvent;
    }

    public class VirtualPropertyHolder
    {
        public virtual int Value { get; set; }
    }

    public class OverridePropertyHolder : VirtualPropertyHolder
    {
        public override int Value { get; set; }
    }

    public class VirtualEventHolder
    {
        public virtual event EventHandler? Changed
        {
            add
            {
            }

            remove
            {
            }
        }
    }

    public class OverrideEventHolder : VirtualEventHolder
    {
        public override event EventHandler? Changed
        {
            add
            {
            }

            remove
            {
            }
        }
    }

    public static class ConstantVariantHolder
    {
        public const bool BoolConst = true;
        public const char CharConst = 'x';
        public const float FloatConst = 1.5f;
        public const double DoubleConst = 2.5d;
        public const int IntConst = 42;
        public const string EscapedStringConst = "line1\nline2\t\"quoted\"\\backslash";
        public const string BracketConst = "foo [bar]";
    }
}

#pragma warning restore CS0649
#pragma warning restore CS0067
