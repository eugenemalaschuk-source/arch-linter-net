using System;

namespace ArchLinterNet.Annotations
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    internal sealed class AnnotationCatalogIdentityAttribute : Attribute
    {
        public AnnotationCatalogIdentityAttribute(int catalogGeneration, string packageVersion)
        {
            CatalogGeneration = catalogGeneration;
            PackageVersion = packageVersion;
        }

        public int CatalogGeneration { get; }

        public string PackageVersion { get; }
    }
}
