using ArchLinterNet.CEL.Binding;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Parsing;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;

namespace ArchLinterNet.CEL.Evaluation;

/// <summary>
/// Internal bounded evaluator for the shipped Profile v1 bound-node set.
/// </summary>
internal static class CelEvaluator
{
    public static CelEvaluationResult Evaluate(
        CelBoundExpression boundExpression,
        CelContextSchema expectedSchema,
        CelProfileId profileId,
        CelEvaluationContext context,
        CelEvaluationLimits limits)
    {
        if (!string.Equals(context.Schema.Identity, expectedSchema.Identity, StringComparison.Ordinal))
        {
            return Failure(CelEvaluationDiagnostics.SchemaMismatch(
                context.Schema.Identity,
                expectedSchema.Identity,
                profileId));
        }

        var session = new EvaluationSession(boundExpression, profileId, context, limits);
        return session.Evaluate();
    }

    private static CelEvaluationResult Success(CelValue value) =>
        new(isSuccess: true, value, diagnostics: []);

    private static CelEvaluationResult Failure(CelDiagnostic diagnostic) =>
        new(isSuccess: false, value: null, diagnostics: [diagnostic]);

    private readonly record struct EvaluationStep(CelValue? Value, CelDiagnostic? Diagnostic)
    {
        public bool IsSuccess => Diagnostic is null;

        public static EvaluationStep Succeeded(CelValue value) => new(value, null);

        public static EvaluationStep Failed(CelDiagnostic diagnostic) => new(null, diagnostic);
    }

    private sealed class EvaluationSession
    {
        private readonly CelBoundExpression _boundExpression;
        private readonly CelProfileId _profileId;
        private readonly CelEvaluationLimits _limits;
        private readonly IReadOnlyDictionary<string, CelValue> _valuesByName;

        private long _iterationsConsumed;
        private long _costConsumed;

        public EvaluationSession(
            CelBoundExpression boundExpression,
            CelProfileId profileId,
            CelEvaluationContext context,
            CelEvaluationLimits limits)
        {
            _boundExpression = boundExpression;
            _profileId = profileId;
            _limits = limits;
            _valuesByName = context.Assignments.ToDictionary(
                assignment => assignment.Variable.Name,
                assignment => assignment.Value,
                StringComparer.Ordinal);
        }

        public CelEvaluationResult Evaluate()
        {
            var step = EvaluateNode(_boundExpression.Root);
            return step.IsSuccess
                ? Success(step.Value!)
                : Failure(step.Diagnostic!);
        }

        private EvaluationStep EvaluateNode(CelBoundNode node)
        {
            var iterationDiagnostic = ChargeIteration(node.Span);
            if (iterationDiagnostic is not null)
                return EvaluationStep.Failed(iterationDiagnostic);

            return node switch
            {
                CelBoundBoolLiteral b => EvaluationStep.Succeeded(CelValue.Bool(b.Value)),
                CelBoundIntLiteral i => EvaluationStep.Succeeded(CelValue.Int(i.Value)),
                CelBoundFloatLiteral f => EvaluationStep.Succeeded(CelValue.Float(f.Value)),
                CelBoundStringLiteral s => EvaluationStep.Succeeded(CelValue.String(s.Value)),
                CelBoundIdentifier id => EvaluateIdentifier(id),
                CelBoundUnary unary => EvaluateUnary(unary),
                CelBoundBinary binary => EvaluateBinary(binary),
                CelBoundMemberAccess memberAccess => EvaluateMemberAccess(memberAccess),
                CelBoundIndex index => EvaluateIndex(index),
                CelBoundCall call => EvaluateCall(call),
                _ => throw new InvalidOperationException(
                    $"Unhandled bound node type '{node.GetType().Name}' reached the evaluator."),
            };
        }

        private EvaluationStep EvaluateIdentifier(CelBoundIdentifier identifier) =>
            _valuesByName.TryGetValue(identifier.Variable.Name, out var value)
                ? EvaluationStep.Succeeded(value)
                : EvaluationStep.Failed(CelEvaluationDiagnostics.MissingVariable(
                    identifier.Span,
                    identifier.Variable.Name,
                    _profileId));

        private EvaluationStep EvaluateUnary(CelBoundUnary unary)
        {
            var operand = EvaluateNode(unary.Operand);
            if (!operand.IsSuccess)
                return operand;

            return unary.Operator switch
            {
                CelUnaryOperator.Not => EvaluationStep.Succeeded(CelValue.Bool(!operand.Value!.AsBool())),
                _ => throw new InvalidOperationException($"Unsupported unary operator '{unary.Operator}'."),
            };
        }

        private EvaluationStep EvaluateBinary(CelBoundBinary binary) => binary.Operator switch
        {
            CelBinaryOperator.And => EvaluateAnd(binary),
            CelBinaryOperator.Or => EvaluateOr(binary),
            CelBinaryOperator.Equal => EvaluateEquality(binary, negate: false),
            CelBinaryOperator.NotEqual => EvaluateEquality(binary, negate: true),
            CelBinaryOperator.Less => EvaluateOrdering(binary, static comparison => comparison < 0),
            CelBinaryOperator.LessOrEqual => EvaluateOrdering(binary, static comparison => comparison <= 0),
            CelBinaryOperator.Greater => EvaluateOrdering(binary, static comparison => comparison > 0),
            CelBinaryOperator.GreaterOrEqual => EvaluateOrdering(binary, static comparison => comparison >= 0),
            CelBinaryOperator.In => EvaluateIn(binary),
            _ => throw new InvalidOperationException($"Unsupported binary operator '{binary.Operator}'."),
        };

        private EvaluationStep EvaluateAnd(CelBoundBinary binary)
        {
            var left = EvaluateNode(binary.Left);
            if (left.IsSuccess)
            {
                if (!left.Value!.AsBool())
                    return EvaluationStep.Succeeded(CelValue.Bool(false));

                return EvaluateBooleanRight(binary.Right, static right => right);
            }

            var right = EvaluateNode(binary.Right);
            if (right.IsSuccess && !right.Value!.AsBool())
                return EvaluationStep.Succeeded(CelValue.Bool(false));

            return left;
        }

        private EvaluationStep EvaluateOr(CelBoundBinary binary)
        {
            var left = EvaluateNode(binary.Left);
            if (left.IsSuccess)
            {
                if (left.Value!.AsBool())
                    return EvaluationStep.Succeeded(CelValue.Bool(true));

                return EvaluateBooleanRight(binary.Right, static right => right);
            }

            var right = EvaluateNode(binary.Right);
            if (right.IsSuccess && right.Value!.AsBool())
                return EvaluationStep.Succeeded(CelValue.Bool(true));

            return left;
        }

        private EvaluationStep EvaluateBooleanRight(CelBoundNode rightNode, Func<bool, bool> projector)
        {
            var right = EvaluateNode(rightNode);
            if (!right.IsSuccess)
                return right;

            return EvaluationStep.Succeeded(CelValue.Bool(projector(right.Value!.AsBool())));
        }

        private EvaluationStep EvaluateEquality(CelBoundBinary binary, bool negate)
        {
            var left = EvaluateNode(binary.Left);
            if (!left.IsSuccess)
                return left;

            var right = EvaluateNode(binary.Right);
            if (!right.IsSuccess)
                return right;

            var areEqual = AreEqual(left.Value!, right.Value!);
            return EvaluationStep.Succeeded(CelValue.Bool(negate ? !areEqual : areEqual));
        }

        private EvaluationStep EvaluateOrdering(CelBoundBinary binary, Func<int, bool> predicate)
        {
            var left = EvaluateNode(binary.Left);
            if (!left.IsSuccess)
                return left;

            var right = EvaluateNode(binary.Right);
            if (!right.IsSuccess)
                return right;

            var comparison = left.Value!.Kind switch
            {
                CelValueKind.Int => left.Value.AsInt().CompareTo(right.Value!.AsInt()),
                CelValueKind.Float => left.Value.AsFloat().CompareTo(right.Value!.AsFloat()),
                _ => throw new InvalidOperationException(
                    $"Ordering evaluation reached unsupported value kind '{left.Value.Kind}'."),
            };

            return EvaluationStep.Succeeded(CelValue.Bool(predicate(comparison)));
        }

        private EvaluationStep EvaluateIn(CelBoundBinary binary)
        {
            var left = EvaluateNode(binary.Left);
            if (!left.IsSuccess)
                return left;

            var right = EvaluateNode(binary.Right);
            if (!right.IsSuccess)
                return right;

            return right.Value!.Kind switch
            {
                CelValueKind.List => EvaluationStep.Succeeded(CelValue.Bool(
                    right.Value.AsList().Any(candidate => AreEqual(left.Value!, candidate)))),
                CelValueKind.Map => EvaluationStep.Succeeded(CelValue.Bool(
                    right.Value.AsMap().ContainsKey(left.Value!.AsString()))),
                _ => throw new InvalidOperationException(
                    $"Operator 'in' reached unsupported right-hand kind '{right.Value.Kind}'."),
            };
        }

        private EvaluationStep EvaluateMemberAccess(CelBoundMemberAccess memberAccess)
        {
            var receiver = EvaluateNode(memberAccess.Receiver);
            if (!receiver.IsSuccess)
                return receiver;

            var obj = receiver.Value!.AsObject();
            return obj.Members.TryGetValue(memberAccess.Member.Name, out var memberValue)
                ? EvaluationStep.Succeeded(memberValue)
                : EvaluationStep.Failed(CelEvaluationDiagnostics.MissingMember(
                    memberAccess.Span,
                    memberAccess.Member.Name,
                    _profileId));
        }

        private EvaluationStep EvaluateIndex(CelBoundIndex index)
        {
            var receiver = EvaluateNode(index.Receiver);
            if (!receiver.IsSuccess)
                return receiver;

            var keyOrIndex = EvaluateNode(index.Index);
            if (!keyOrIndex.IsSuccess)
                return keyOrIndex;

            return receiver.Value!.Kind switch
            {
                CelValueKind.List => EvaluateListIndex(index, receiver.Value.AsList(), keyOrIndex.Value!.AsInt()),
                CelValueKind.Map => EvaluateMapIndex(index, receiver.Value.AsMap(), keyOrIndex.Value!.AsString()),
                _ => throw new InvalidOperationException(
                    $"Index evaluation reached unsupported receiver kind '{receiver.Value.Kind}'."),
            };
        }

        private EvaluationStep EvaluateListIndex(CelBoundIndex index, IReadOnlyList<CelValue> receiver, long valueIndex)
        {
            if (valueIndex < 0 || valueIndex >= receiver.Count)
            {
                return EvaluationStep.Failed(CelEvaluationDiagnostics.InvalidIndex(
                    index.Span,
                    valueIndex,
                    receiver.Count,
                    _profileId));
            }

            return EvaluationStep.Succeeded(receiver[(int)valueIndex]);
        }

        private EvaluationStep EvaluateMapIndex(
            CelBoundIndex index,
            IReadOnlyDictionary<string, CelValue> receiver,
            string key) =>
            receiver.TryGetValue(key, out var value)
                ? EvaluationStep.Succeeded(value)
                : EvaluationStep.Failed(CelEvaluationDiagnostics.MissingKey(index.Span, key, _profileId));

        private EvaluationStep EvaluateCall(CelBoundCall call)
        {
            CelValue? receiver = null;
            if (call.Receiver is not null)
            {
                var receiverStep = EvaluateNode(call.Receiver);
                if (!receiverStep.IsSuccess)
                    return receiverStep;

                receiver = receiverStep.Value;
            }

            var arguments = new List<CelValue>(call.Arguments.Count);
            foreach (var argument in call.Arguments)
            {
                var argumentStep = EvaluateNode(argument);
                if (!argumentStep.IsSuccess)
                    return argumentStep;

                arguments.Add(argumentStep.Value!);
            }

            var cost = CelBuiltinFunctionInvoker.ComputeCost(call.Overload.OperationId, receiver, arguments);
            var costDiagnostic = ChargeCost(call.Span, cost);
            if (costDiagnostic is not null)
                return EvaluationStep.Failed(costDiagnostic);

            return EvaluationStep.Succeeded(CelBuiltinFunctionInvoker.Invoke(
                call.Overload.OperationId,
                receiver,
                arguments));
        }

        private CelDiagnostic? ChargeIteration(CelSourceSpan span)
        {
            _iterationsConsumed++;
            return _iterationsConsumed > _limits.MaxIterations
                ? CelEvaluationDiagnostics.BudgetExceeded(span, "MaxIterations", _iterationsConsumed, _profileId)
                : null;
        }

        private CelDiagnostic? ChargeCost(CelSourceSpan span, long charge)
        {
            _costConsumed = long.MaxValue - _costConsumed < charge
                ? long.MaxValue
                : _costConsumed + charge;

            return _costConsumed > _limits.MaxCostUnits
                ? CelEvaluationDiagnostics.BudgetExceeded(span, "MaxCostUnits", _costConsumed, _profileId)
                : null;
        }

        private static bool AreEqual(CelValue left, CelValue right)
        {
            if (left.Kind != right.Kind)
                return false;

            return left.Kind switch
            {
                CelValueKind.Bool => left.AsBool() == right.AsBool(),
                CelValueKind.String => string.Equals(left.AsString(), right.AsString(), StringComparison.Ordinal),
                CelValueKind.Int => left.AsInt() == right.AsInt(),
                CelValueKind.Float => left.AsFloat().Equals(right.AsFloat()),
                CelValueKind.List => AreEqual(left.AsList(), right.AsList()),
                CelValueKind.Map => AreEqual(left.AsMap(), right.AsMap()),
                CelValueKind.Object => AreEqual(left.AsObject(), right.AsObject()),
                _ => throw new InvalidOperationException($"Unhandled CEL value kind '{left.Kind}'."),
            };
        }

        private static bool AreEqual(IReadOnlyList<CelValue> left, IReadOnlyList<CelValue> right)
        {
            if (left.Count != right.Count)
                return false;

            for (var i = 0; i < left.Count; i++)
            {
                if (!AreEqual(left[i], right[i]))
                    return false;
            }

            return true;
        }

        private static bool AreEqual(
            IReadOnlyDictionary<string, CelValue> left,
            IReadOnlyDictionary<string, CelValue> right)
        {
            if (left.Count != right.Count)
                return false;

            foreach (var (key, value) in left)
            {
                if (!right.TryGetValue(key, out var rightValue) || !AreEqual(value, rightValue))
                    return false;
            }

            return true;
        }

        private static bool AreEqual(CelObjectValue left, CelObjectValue right) =>
            string.Equals(left.ObjectTypeId, right.ObjectTypeId, StringComparison.Ordinal) &&
            AreEqual(left.Members, right.Members);
    }
}
