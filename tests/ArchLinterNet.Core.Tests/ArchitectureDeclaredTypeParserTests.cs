using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDeclaredTypeParserTests
{
    private static readonly string[] _featureXSymbols = ["FEATURE_X"];
    private static readonly string[] _legacySymbols = ["LEGACY"];
    private static readonly string[] _nestedTypeNames = ["MyApp.Domain.Order", "MyApp.Domain.Order+LineItem"];
    private static readonly string[] _multipleTypeNames = ["MyApp.Domain.Order", "MyApp.Domain.IOrderService", "MyApp.Domain.OrderStatus"];
    private static readonly string[] _nestedGenericNames = ["MyApp.Outer`1", "MyApp.Outer`1+Inner`1"];
    private static readonly string[] _legacyImplNames = ["MyApp.LegacyImpl"];
    private static readonly string[] _modernImplNames = ["MyApp.ModernImpl"];
    [Test]
    public void ParseSourceText_SingleClass_ReturnsClassFact()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp.Domain { public class Order { } }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.Domain.Order"));
        Assert.That(types[0].SimpleTypeName, Is.EqualTo("Order"));
        Assert.That(types[0].Namespace, Is.EqualTo("MyApp.Domain"));
        Assert.That(types[0].TypeKind, Is.EqualTo(ArchitectureTypeKind.Class));
    }

    [Test]
    public void ParseSourceText_SingleInterface_ReturnsInterfaceFact()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp { public interface IOrderRepository { } }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.IOrderRepository"));
        Assert.That(types[0].TypeKind, Is.EqualTo(ArchitectureTypeKind.Interface));
    }

    [Test]
    public void ParseSourceText_SingleStruct_ReturnsStructFact()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp { public struct Money { } }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.Money"));
        Assert.That(types[0].TypeKind, Is.EqualTo(ArchitectureTypeKind.Struct));
    }

    [Test]
    public void ParseSourceText_SingleEnum_ReturnsEnumFact()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp { public enum OrderStatus { New, Shipped } }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.OrderStatus"));
        Assert.That(types[0].TypeKind, Is.EqualTo(ArchitectureTypeKind.Enum));
    }

    [Test]
    public void ParseSourceText_SingleDelegate_ReturnsDelegateFact()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp { public delegate void OrderHandler(string id); }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.OrderHandler"));
        Assert.That(types[0].TypeKind, Is.EqualTo(ArchitectureTypeKind.Delegate));
    }

    [Test]
    public void ParseSourceText_RecordClass_ReturnsRecordFact()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp { public record OrderDto(string Id); }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.OrderDto"));
        Assert.That(types[0].TypeKind, Is.EqualTo(ArchitectureTypeKind.Record));
    }

    [Test]
    public void ParseSourceText_RecordStruct_ReturnsRecordFact()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp { public record struct Point(int X, int Y); }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.Point"));
        Assert.That(types[0].TypeKind, Is.EqualTo(ArchitectureTypeKind.Record));
    }

    [Test]
    public void ParseSourceText_FileScopedNamespace_ExtractsCorrectFullName()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp.Domain;
                public class Product { }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.Domain.Product"));
        Assert.That(types[0].Namespace, Is.EqualTo("MyApp.Domain"));
        Assert.That(types[0].TypeKind, Is.EqualTo(ArchitectureTypeKind.Class));
    }

    [Test]
    public void ParseSourceText_NestedType_UsesClrPlusFormat()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp.Domain {
                    public class Order {
                        public class LineItem { }
                    }
                }
                """);

        Assert.That(types.Select(t => t.FullTypeName), Is.EquivalentTo(_nestedTypeNames));

        ArchitectureDeclaredTypeParser.ParsedTypeInfo innerFact =
            types.Single(t => t.SimpleTypeName == "LineItem");
        Assert.That(innerFact.FullTypeName, Is.EqualTo("MyApp.Domain.Order+LineItem"));
        Assert.That(innerFact.Namespace, Is.EqualTo("MyApp.Domain"));
        Assert.That(innerFact.TypeKind, Is.EqualTo(ArchitectureTypeKind.Class));
    }

    [Test]
    public void ParseSourceText_GenericType_AppendsBacktickArity()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp { public class Repository<T> { } }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.Repository`1"));
        Assert.That(types[0].SimpleTypeName, Is.EqualTo("Repository"));
    }

    [Test]
    public void ParseSourceText_GenericTypeWithTwoParams_AppendBacktickTwo()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp { public class Map<TKey, TValue> { } }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.Map`2"));
    }

    [Test]
    public void ParseSourceText_MultipleTypesInFile_ReturnsAllFacts()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp.Domain {
                    public class Order { }
                    public interface IOrderService { }
                    public enum OrderStatus { New }
                }
                """);

        Assert.That(types, Has.Count.EqualTo(3));
        Assert.That(types.Select(t => t.FullTypeName), Is.EquivalentTo(_multipleTypeNames));
    }

    [Test]
    public void ParseSourceText_GenericDelegate_AppendsBacktickArity()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp { public delegate T Transformer<T>(string input); }
                """);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.Transformer`1"));
        Assert.That(types[0].TypeKind, Is.EqualTo(ArchitectureTypeKind.Delegate));
    }

    [Test]
    public void ParseSourceText_NestedGenericInGeneric_ProducesClrFormatName()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("""
                namespace MyApp {
                    public class Outer<T> {
                        public class Inner<U> { }
                    }
                }
                """);

        Assert.That(types.Select(t => t.FullTypeName), Is.EquivalentTo(_nestedGenericNames));
    }

    [Test]
    public void ParseSourceText_GlobalNamespace_OmitsNamespacePrefix()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText("public class GlobalType { }");

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("GlobalType"));
        Assert.That(types[0].Namespace, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ParseSourceText_EmptySource_ReturnsEmptyList()
    {
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText(string.Empty);

        Assert.That(types, Is.Empty);
    }

    // ── Preprocessor symbols (3.2) ────────────────────────────────────────────────────

    [Test]
    public void ParseSourceText_WithPreprocessorSymbol_IncludesConditionalType()
    {
        const string Source = """
            namespace MyApp {
            #if FEATURE_X
                public class ConditionalType { }
            #endif
            }
            """;

        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText(Source, _featureXSymbols);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.ConditionalType"));
    }

    [Test]
    public void ParseSourceText_WithoutPreprocessorSymbol_ExcludesConditionalType()
    {
        const string Source = """
            namespace MyApp {
            #if FEATURE_X
                public class ConditionalType { }
            #endif
                public class AlwaysPresent { }
            }
            """;

        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText(Source);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.AlwaysPresent"));
    }

    [Test]
    public void ParseSourceText_MutuallyExclusiveConditionals_OnlyActiveSymbolTypeIncluded()
    {
        const string Source = """
            namespace MyApp {
            #if LEGACY
                public class LegacyImpl { }
            #else
                public class ModernImpl { }
            #endif
            }
            """;

        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> withLegacy =
            ArchitectureDeclaredTypeParser.ParseSourceText(Source, _legacySymbols);
        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> withoutLegacy =
            ArchitectureDeclaredTypeParser.ParseSourceText(Source);

        Assert.That(withLegacy.Select(t => t.FullTypeName), Is.EquivalentTo(_legacyImplNames));
        Assert.That(withoutLegacy.Select(t => t.FullTypeName), Is.EquivalentTo(_modernImplNames));
    }

    // ── Escaped identifiers (4.3) ─────────────────────────────────────────────────────

    [Test]
    public void ParseSourceText_EscapedKeywordIdentifier_DecodesWithoutAtSign()
    {
        // @class is a valid C# identifier that escapes the keyword "class".
        // Reflection uses the decoded name (without @), so the parser must match.
        const string Source = "namespace MyApp { public class @class { } }";

        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText(Source);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("MyApp.class"));
        Assert.That(types[0].SimpleTypeName, Is.EqualTo("class"));
    }

    [Test]
    public void ParseSourceText_EscapedKeywordNamespace_DecodesWithoutAtSign()
    {
        const string Source = "namespace @namespace { public class Foo { } }";

        IReadOnlyList<ArchitectureDeclaredTypeParser.ParsedTypeInfo> types =
            ArchitectureDeclaredTypeParser.ParseSourceText(Source);

        Assert.That(types, Has.Count.EqualTo(1));
        Assert.That(types[0].FullTypeName, Is.EqualTo("namespace.Foo"));
        Assert.That(types[0].Namespace, Is.EqualTo("namespace"));
    }
}
