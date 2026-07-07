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

        public void DoWork()
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

        public void UndeclaredMethod()
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

        protected void ProtectedMethod()
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
}

#pragma warning restore CS0649
#pragma warning restore CS0067
