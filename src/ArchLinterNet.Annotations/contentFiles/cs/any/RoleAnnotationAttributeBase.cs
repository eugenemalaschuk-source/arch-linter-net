#nullable enable

using System;

namespace ArchLinterNet.Annotations
{

    internal abstract class RoleAnnotationAttributeBase : Attribute
    {
        public string? Domain { get; set; }

        public string? BoundedContext { get; set; }

        public string? Module { get; set; }

        public string? Feature { get; set; }

        public string? Platform { get; set; }

        public string? Runtime { get; set; }
    }
}
