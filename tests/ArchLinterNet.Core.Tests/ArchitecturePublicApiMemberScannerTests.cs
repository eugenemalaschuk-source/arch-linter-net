using System.Runtime.CompilerServices;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePublicApiMemberScannerTests
{
    [Test]
    public void GetExportedSurface_PreservesMemberTraversalOrderAndExportedVisibility()
    {
        string declaringTypeName = typeof(ExportedMembers).FullName!;
        IReadOnlyList<ArchitectureExportedApiEntry> members =
            ArchitecturePublicApiSurfaceScanner.GetExportedSurface(typeof(ExportedMembers).Assembly)
                .Where(entry => entry.DeclaringTypeName == declaringTypeName)
                .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(members.Select(entry => entry.Signature), Is.EqualTo(new[]
            {
                $"class {declaringTypeName}",
                $"ctor {declaringTypeName}()",
                $"method {declaringTypeName}.PublicMethod(System.Int32): System.String",
                $"method {declaringTypeName}.ProtectedMethod(System.String): System.String",
                $"method {declaringTypeName}.ProtectedInternalMethod(): System.Void",
                $"property {declaringTypeName}.PublicProperty: System.String",
                $"property {declaringTypeName}.ProtectedProperty: System.String",
                $"property {declaringTypeName}.ProtectedInternalProperty: System.String",
                $"field {declaringTypeName}.PublicField: System.Int32",
                $"field {declaringTypeName}.ProtectedField: System.Int32",
                $"field {declaringTypeName}.ProtectedInternalField: System.Int32",
                $"event {declaringTypeName}.PublicEvent: System.EventHandler",
                $"event {declaringTypeName}.ProtectedEvent: System.EventHandler",
                $"event {declaringTypeName}.ProtectedInternalEvent: System.EventHandler"
            }));
            Assert.That(members.Skip(1).Select(entry => entry.Visibility), Is.EqualTo(new[]
            {
                "public",
                "public",
                "protected",
                "protected internal",
                "public",
                "protected",
                "protected internal",
                "public",
                "protected",
                "protected internal",
                "public",
                "protected",
                "protected internal"
            }));
            Assert.That(members.Select(entry => entry.Signature), Has.None.Contains("InternalOnly"));
            Assert.That(members.Select(entry => entry.Signature), Has.None.Contains("PrivateOnly"));
        });
    }

    [Test]
    public void GetExportedTypes_ExcludesCompilerGeneratedTypes()
    {
        IReadOnlyList<Type> exportedTypes = ArchitecturePublicApiSurfaceScanner
            .GetExportedTypes(typeof(ArchitecturePublicApiMemberScannerTests).Assembly)
            .ToArray();

        Assert.That(exportedTypes, Does.Not.Contain(typeof(CompilerGeneratedExportedType)));
    }

    public class ExportedMembers
    {
        public ExportedMembers()
        {
        }

        public string PublicMethod(int value) => value.ToString();

        protected string ProtectedMethod(string value) => value;

        protected internal void ProtectedInternalMethod()
        {
        }

        internal void InternalOnly()
        {
        }

        private void PrivateOnly()
        {
        }

        public string PublicProperty { get; set; } = string.Empty;

        protected string ProtectedProperty { get; set; } = string.Empty;

        protected internal string ProtectedInternalProperty { get; set; } = string.Empty;

        internal string InternalProperty { get; set; } = string.Empty;

        private string PrivateProperty { get; set; } = string.Empty;

        public int PublicField;

        protected int ProtectedField;

        protected internal int ProtectedInternalField;

        internal int InternalField = 0;

        public event EventHandler? PublicEvent
        {
            add { }
            remove { }
        }

        protected event EventHandler? ProtectedEvent
        {
            add { }
            remove { }
        }

        protected internal event EventHandler? ProtectedInternalEvent
        {
            add { }
            remove { }
        }

        internal event EventHandler? InternalEvent
        {
            add { }
            remove { }
        }

        private event EventHandler? PrivateEvent
        {
            add { }
            remove { }
        }
    }

    [CompilerGenerated]
    public sealed class CompilerGeneratedExportedType
    {
    }
}
