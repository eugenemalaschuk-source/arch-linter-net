using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDeclaredTypeParserTests
{
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

        Assert.That(types.Select(t => t.FullTypeName), Is.EquivalentTo(new[]
        {
            "MyApp.Domain.Order",
            "MyApp.Domain.Order+LineItem"
        }));

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
        Assert.That(types.Select(t => t.FullTypeName), Is.EquivalentTo(new[]
        {
            "MyApp.Domain.Order",
            "MyApp.Domain.IOrderService",
            "MyApp.Domain.OrderStatus"
        }));
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

        Assert.That(types.Select(t => t.FullTypeName), Is.EquivalentTo(new[]
        {
            "MyApp.Outer`1",
            "MyApp.Outer`1+Inner`1"
        }));
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
}
