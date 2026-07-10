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
}

#pragma warning restore CS0649
#pragma warning restore CS0067
