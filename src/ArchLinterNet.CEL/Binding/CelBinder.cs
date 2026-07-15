using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Parsing;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// Resolves identifiers, members, indices, and calls against the compilation's
/// <see cref="CelContextSchema"/>/object-schema catalog, statically type-checks every operator
/// against the frozen Profile v1 signature table, and produces an immutable
/// <see cref="CelBoundExpression"/> on success.
/// </summary>
/// <remarks>
/// <para>
/// Binds the whole syntax tree unconditionally — every child of every node is visited regardless
/// of the parent operator's evaluation-time short-circuit semantics, so a violation in a branch a
/// future evaluator might never evaluate at runtime is still diagnosable at compile time.
/// </para>
/// <para>
/// Fails fast, matching <see cref="CelParser"/>'s contract: the first binding or type-check
/// violation encountered in a fixed left-to-right, depth-first walk stops binding and is returned
/// as the single diagnostic (via exception) — no diagnostic aggregation.
/// </para>
/// </remarks>
internal sealed class CelBinder
{
    private readonly CelContextSchema _schema;
    private readonly IReadOnlyDictionary<string, CelObjectSchema> _objectSchemas;
    private readonly CelProfileId _profileId;

    private CelBinder(CelContextSchema schema, IReadOnlyDictionary<string, CelObjectSchema> objectSchemas, CelProfileId profileId)
    {
        _schema = schema;
        _objectSchemas = objectSchemas;
        _profileId = profileId;
    }

    public static CelBindResult Bind(
        CelSyntaxNode root,
        CelContextSchema schema,
        IReadOnlyDictionary<string, CelObjectSchema> objectSchemas,
        CelRequiredResultType requiredResultType,
        CelProfileId profileId)
    {
        var binder = new CelBinder(schema, objectSchemas, profileId);
        try
        {
            var bound = binder.BindNode(root);
            binder.CheckRequiredResultType(bound, requiredResultType);
            return CelBindResult.Success(new CelBoundExpression(bound));
        }
        catch (CelBindException ex)
        {
            return CelBindResult.Failed(ex.Diagnostic);
        }
    }

    private CelBoundNode BindNode(CelSyntaxNode node) => node switch
    {
        CelBoolLiteralSyntax b => new CelBoundBoolLiteral(b.Span, b.Value),
        CelIntLiteralSyntax i => new CelBoundIntLiteral(i.Span, i.Value),
        CelFloatLiteralSyntax f => new CelBoundFloatLiteral(f.Span, f.Value),
        CelStringLiteralSyntax s => new CelBoundStringLiteral(s.Span, s.Value),
        CelIdentifierSyntax id => BindIdentifier(id),
        CelUnarySyntax u => BindUnary(u),
        CelBinarySyntax bin => BindBinary(bin),
        CelMemberAccessSyntax m => BindMemberAccess(m),
        CelIndexSyntax ix => BindIndex(ix),
        CelCallSyntax c => BindCall(c),
        _ => throw new InvalidOperationException(
            $"Unexpected syntax node type '{node.GetType().Name}' reached the binder — " +
            "CelDeferredSyntax never appears in a successfully parsed tree."),
    };

    private CelBoundIdentifier BindIdentifier(CelIdentifierSyntax node)
    {
        var variable = _schema.Variables.FirstOrDefault(v => string.Equals(v.Name, node.Name, StringComparison.Ordinal));
        if (variable is null)
        {
            throw Fail(CelBindDiagnostics.BindingError(
                node.Span, $"Unresolved identifier '{node.Name}'.", _profileId, node.Name));
        }
        return new CelBoundIdentifier(node.Span, variable);
    }

    private CelBoundUnary BindUnary(CelUnarySyntax node)
    {
        var operand = BindNode(node.Operand);
        if (node.Operator != CelUnaryOperator.Not)
            throw new InvalidOperationException($"Unsupported unary operator '{node.Operator}'.");
        if (operand.ResolvedType.Kind != CelTypeKind.Bool)
        {
            throw Fail(CelBindDiagnostics.TypeMismatch(
                node.Span,
                "Operator '!' requires a Bool operand.",
                _profileId,
                "Bool",
                CelTypeDisplay.Describe(operand.ResolvedType)));
        }
        return new CelBoundUnary(node.Span, CelType.Bool, node.Operator, operand);
    }

    private CelBoundBinary BindBinary(CelBinarySyntax node)
    {
        // Whole-AST binding: both operands are bound unconditionally, regardless of what a future
        // evaluator's short-circuit/error-absorbing behavior might skip at runtime.
        var left = BindNode(node.Left);
        var right = BindNode(node.Right);
        return node.Operator switch
        {
            CelBinaryOperator.And or CelBinaryOperator.Or => BindLogical(node, left, right),
            CelBinaryOperator.Equal or CelBinaryOperator.NotEqual => BindEquality(node, left, right),
            CelBinaryOperator.Less or CelBinaryOperator.LessOrEqual
                or CelBinaryOperator.Greater or CelBinaryOperator.GreaterOrEqual => BindOrdering(node, left, right),
            CelBinaryOperator.In => BindIn(node, left, right),
            _ => throw new InvalidOperationException($"Unsupported binary operator '{node.Operator}'."),
        };
    }

    private CelBoundBinary BindLogical(CelBinarySyntax node, CelBoundNode left, CelBoundNode right)
    {
        if (left.ResolvedType.Kind != CelTypeKind.Bool)
        {
            throw Fail(CelBindDiagnostics.TypeMismatch(
                left.Span,
                $"Operator '{OperatorText(node.Operator)}' requires Bool operands.",
                _profileId,
                "Bool",
                CelTypeDisplay.Describe(left.ResolvedType)));
        }
        if (right.ResolvedType.Kind != CelTypeKind.Bool)
        {
            throw Fail(CelBindDiagnostics.TypeMismatch(
                right.Span,
                $"Operator '{OperatorText(node.Operator)}' requires Bool operands.",
                _profileId,
                "Bool",
                CelTypeDisplay.Describe(right.ResolvedType)));
        }
        return new CelBoundBinary(node.Span, CelType.Bool, node.Operator, left, right);
    }

    private CelBoundBinary BindEquality(CelBinarySyntax node, CelBoundNode left, CelBoundNode right)
    {
        if (left.ResolvedType.Kind != right.ResolvedType.Kind)
        {
            throw Fail(CelBindDiagnostics.TypeMismatch(
                node.Span,
                $"Operator '{OperatorText(node.Operator)}' requires two operands of the same type.",
                _profileId,
                CelTypeDisplay.Describe(left.ResolvedType),
                CelTypeDisplay.Describe(right.ResolvedType)));
        }
        return new CelBoundBinary(node.Span, CelType.Bool, node.Operator, left, right);
    }

    private CelBoundBinary BindOrdering(CelBinarySyntax node, CelBoundNode left, CelBoundNode right)
    {
        var leftIsNumeric = left.ResolvedType.Kind is CelTypeKind.Int or CelTypeKind.Float;
        if (!leftIsNumeric || left.ResolvedType.Kind != right.ResolvedType.Kind)
        {
            var expected = leftIsNumeric ? CelTypeDisplay.Describe(left.ResolvedType) : "Int or Float";
            throw Fail(CelBindDiagnostics.TypeMismatch(
                node.Span,
                $"Operator '{OperatorText(node.Operator)}' requires two operands of the same numeric " +
                "type (Int or Float, no implicit widening).",
                _profileId,
                expected,
                CelTypeDisplay.Describe(right.ResolvedType)));
        }
        return new CelBoundBinary(node.Span, CelType.Bool, node.Operator, left, right);
    }

    private CelBoundBinary BindIn(CelBinarySyntax node, CelBoundNode left, CelBoundNode right)
    {
        var matches = right.ResolvedType.Kind switch
        {
            CelTypeKind.List => CelTypeEquality.AreEqual(left.ResolvedType, right.ResolvedType.ElementType!),
            CelTypeKind.Map => left.ResolvedType.Kind == CelTypeKind.String,
            _ => false,
        };
        if (!matches)
        {
            throw Fail(CelBindDiagnostics.TypeMismatch(
                node.Span,
                "Operator 'in' requires 'T in List<T>' or 'String in Map<String, T>'.",
                _profileId,
                ExpectedInDescription(right.ResolvedType),
                CelTypeDisplay.Describe(left.ResolvedType)));
        }
        return new CelBoundBinary(node.Span, CelType.Bool, node.Operator, left, right);
    }

    private static string ExpectedInDescription(CelType rightType) => rightType.Kind switch
    {
        CelTypeKind.List => CelTypeDisplay.Describe(rightType.ElementType!),
        CelTypeKind.Map => "String",
        _ => "List<T> or Map<String, T>",
    };

    private CelBoundMemberAccess BindMemberAccess(CelMemberAccessSyntax node)
    {
        var receiver = BindNode(node.Receiver);
        if (receiver.ResolvedType.Kind != CelTypeKind.Object)
        {
            throw Fail(CelBindDiagnostics.TypeMismatch(
                node.Span,
                "Member access requires an Object receiver.",
                _profileId,
                "Object",
                CelTypeDisplay.Describe(receiver.ResolvedType)));
        }
        var schemaId = receiver.ResolvedType.SchemaId!;
        CelObjectMember? member = _objectSchemas.TryGetValue(schemaId, out var objectSchema)
            ? objectSchema.Members.FirstOrDefault(m => string.Equals(m.Name, node.MemberName, StringComparison.Ordinal))
            : null;
        if (member is null)
        {
            throw Fail(CelBindDiagnostics.SchemaMismatch(
                node.Span,
                $"Undeclared member '{node.MemberName}' on object type '{schemaId}'.",
                _profileId,
                node.MemberName));
        }
        return new CelBoundMemberAccess(node.Span, receiver, member);
    }

    private CelBoundIndex BindIndex(CelIndexSyntax node)
    {
        var receiver = BindNode(node.Receiver);
        var index = BindNode(node.Index);
        switch (receiver.ResolvedType.Kind)
        {
            case CelTypeKind.List:
                if (index.ResolvedType.Kind != CelTypeKind.Int)
                {
                    throw Fail(CelBindDiagnostics.TypeMismatch(
                        node.Span,
                        "List indexing requires an Int index.",
                        _profileId,
                        "Int",
                        CelTypeDisplay.Describe(index.ResolvedType)));
                }
                return new CelBoundIndex(node.Span, receiver.ResolvedType.ElementType!, receiver, index);
            case CelTypeKind.Map:
                if (index.ResolvedType.Kind != CelTypeKind.String)
                {
                    throw Fail(CelBindDiagnostics.TypeMismatch(
                        node.Span,
                        "Map indexing requires a String key.",
                        _profileId,
                        "String",
                        CelTypeDisplay.Describe(index.ResolvedType)));
                }
                return new CelBoundIndex(node.Span, receiver.ResolvedType.ValueType!, receiver, index);
            default:
                throw Fail(CelBindDiagnostics.TypeMismatch(
                    node.Span,
                    "Indexing requires a List or Map receiver.",
                    _profileId,
                    "List or Map",
                    CelTypeDisplay.Describe(receiver.ResolvedType)));
        }
    }

    private CelBoundCall BindCall(CelCallSyntax node)
    {
        var receiver = node.Receiver is null ? null : BindNode(node.Receiver);
        var arguments = node.Arguments.Select(BindNode).ToList();

        if (!CelFunctionCatalog.HasAnyOverload(node.FunctionName))
        {
            throw Fail(CelBindDiagnostics.BindingError(
                node.Span, $"Unknown function '{node.FunctionName}'.", _profileId, node.FunctionName));
        }

        var overloads = CelFunctionCatalog.OverloadsFor(node.FunctionName).ToList();
        var arityMatches = overloads.Where(o => o.ArgumentKinds.Count == arguments.Count).ToList();
        if (arityMatches.Count == 0)
        {
            throw Fail(CelBindDiagnostics.BindingError(
                node.Span,
                $"Function '{node.FunctionName}' does not accept {arguments.Count} argument(s).",
                _profileId,
                node.FunctionName));
        }

        var receiverKind = receiver?.ResolvedType.Kind;
        var matched = arityMatches.FirstOrDefault(o =>
            o.ReceiverKind == receiverKind &&
            ArgumentKindsMatch(o.ArgumentKinds, arguments));

        if (matched is null)
        {
            var expectedReceiver = string.Join(" or ", arityMatches.Select(o => o.ReceiverKind?.ToString() ?? "none").Distinct());
            var actualReceiver = receiverKind?.ToString() ?? "none";
            throw Fail(CelBindDiagnostics.TypeMismatch(
                node.Span,
                $"Function '{node.FunctionName}' has no overload matching the given receiver/argument types.",
                _profileId,
                expectedReceiver,
                actualReceiver));
        }

        return new CelBoundCall(node.Span, receiver, node.FunctionName, arguments, matched);
    }

    private static bool ArgumentKindsMatch(IReadOnlyList<CelTypeKind> expected, IReadOnlyList<CelBoundNode> actual)
    {
        for (var i = 0; i < expected.Count; i++)
        {
            if (expected[i] != actual[i].ResolvedType.Kind)
                return false;
        }
        return true;
    }

    private void CheckRequiredResultType(CelBoundNode root, CelRequiredResultType requiredResultType)
    {
        if (requiredResultType == CelRequiredResultType.Predicate && root.ResolvedType.Kind != CelTypeKind.Bool)
        {
            throw Fail(CelBindDiagnostics.TypeMismatch(
                root.Span,
                "A predicate compilation requires the expression's result type to be Bool.",
                _profileId,
                "Bool",
                CelTypeDisplay.Describe(root.ResolvedType)));
        }
    }

    private static string OperatorText(CelBinaryOperator op) => op switch
    {
        CelBinaryOperator.And => "&&",
        CelBinaryOperator.Or => "||",
        CelBinaryOperator.Equal => "==",
        CelBinaryOperator.NotEqual => "!=",
        CelBinaryOperator.Less => "<",
        CelBinaryOperator.LessOrEqual => "<=",
        CelBinaryOperator.Greater => ">",
        CelBinaryOperator.GreaterOrEqual => ">=",
        CelBinaryOperator.In => "in",
        _ => op.ToString(),
    };

    private static CelBindException Fail(CelDiagnostic diagnostic) => new(diagnostic);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "S3871:Exception types should be \"public\"",
        Justification = "Internal control-flow signal local to CelBinder's recursive bind pass; " +
            "never crosses the assembly boundary and is always caught inside Bind() before a " +
            "result is returned.")]
    private sealed class CelBindException : Exception
    {
        public CelDiagnostic Diagnostic { get; }

        public CelBindException(CelDiagnostic diagnostic) => Diagnostic = diagnostic;
    }
}
