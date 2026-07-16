using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// The immutable, internal bound-expression tree produced by a successful <see cref="CelBinder"/>
/// run. Held only by <c>CelCompiledPredicate</c>/<c>CelCompiledExpression</c> — never exposed
/// publicly.
/// </summary>
internal sealed class CelBoundExpression
{
    /// <summary>Gets the root of the bound-expression tree.</summary>
    public CelBoundNode Root { get; }

    /// <summary>Gets the root expression's statically resolved type.</summary>
    public CelType ResolvedType => Root.ResolvedType;

    public CelBoundExpression(CelBoundNode root)
    {
        Root = root;
    }
}
