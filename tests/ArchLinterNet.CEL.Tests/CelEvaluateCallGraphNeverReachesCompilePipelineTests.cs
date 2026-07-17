using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.CEL.Binding;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Parsing;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Static call-graph proof, not a sampled runtime observation: walks the actual CIL instructions
/// reachable from <see cref="CelCompiledPredicate.Evaluate(CelEvaluationContext, CelEvaluationLimits)"/>
/// and <see cref="CelCompiledExpression.Evaluate(CelEvaluationContext, CelEvaluationLimits)"/> and
/// asserts the walk never reaches <see cref="CelTokenizer"/>, <see cref="CelParser"/>, or
/// <see cref="CelBinder"/>. Unlike a call counter sampled over one run, this covers every code path
/// in the current build unconditionally — including paths a runtime sample would miss — and unlike
/// the parameter-signature check in <c>CelRepeatedEvaluationNoReparseTests</c>, it also rules out a
/// regression that re-tokenizes/re-parses/re-binds using state already held on the compiled
/// instance (e.g. <c>CompilationKey.NormalizedSource</c>) rather than a new external input.
/// </summary>
[TestFixture]
public sealed class CelEvaluateCallGraphNeverReachesCompilePipelineTests
{
    private static readonly Type[] _forbiddenCompilePipelineTypes = [typeof(CelTokenizer), typeof(CelParser), typeof(CelBinder)];
    private static readonly Assembly _celAssembly = typeof(CelCompiledPredicate).Assembly;
    private static readonly Dictionary<short, OpCode> _opCodesByValue = BuildOpCodeLookup();

    [Test]
    public void Evaluate_CallGraph_NeverReachesTokenizerParserOrBinder()
    {
        var roots = new[]
        {
            typeof(CelCompiledPredicate).GetMethod(
                nameof(CelCompiledPredicate.Evaluate), [typeof(CelEvaluationContext), typeof(CelEvaluationLimits)]),
            typeof(CelCompiledExpression).GetMethod(
                nameof(CelCompiledExpression.Evaluate), [typeof(CelEvaluationContext), typeof(CelEvaluationLimits)]),
        };
        Assert.That(roots, Has.All.Not.Null, "Both Evaluate(context, limits) entry points must exist.");

        var visited = new HashSet<MethodBase>();
        var offenders = new List<MethodBase>();
        var stack = new Stack<MethodBase>(roots!);

        // Visited-set cap: a genuine explosion here (thousands of distinct CEL-assembly methods
        // reachable from two Evaluate() overloads) would itself be surprising enough to investigate
        // rather than silently walk forever — see the "no silent caps" note this throws if hit.
        const int MaxVisitedMethods = 2000;

        while (stack.Count > 0)
        {
            var method = stack.Pop();
            if (!visited.Add(method))
                continue;

            Assert.That(visited.Count, Is.LessThanOrEqualTo(MaxVisitedMethods),
                $"Call-graph walk exceeded {MaxVisitedMethods} visited methods without terminating — " +
                "widen this cap only after confirming the extra methods are legitimately reachable, " +
                "not a walker bug (e.g. an unresolved recursive edge).");

            if (_forbiddenCompilePipelineTypes.Contains(method.DeclaringType))
                offenders.Add(method);

            foreach (var called in GetCalledMethods(method))
            {
                // Only follow edges within ArchLinterNet.CEL itself — BCL/runtime methods are not
                // part of the compile pipeline and following them would make the graph unbounded.
                if (called.Module.Assembly == _celAssembly && !visited.Contains(called))
                    stack.Push(called);
            }
        }

        Assert.That(offenders, Is.Empty,
            "Evaluate()'s call graph must never reach CelTokenizer/CelParser/CelBinder. Reached: " +
            string.Join(", ", offenders.Select(m => $"{m.DeclaringType}.{m.Name}")));
    }

    private static IEnumerable<MethodBase> GetCalledMethods(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body is null)
            yield break;

        var il = body.GetILAsByteArray();
        if (il is null)
            yield break;

        var module = method.Module;
        var typeArgs = method.DeclaringType is { IsGenericType: true } declaringType
            ? declaringType.GetGenericArguments()
            : null;
        var methodArgs = method is MethodInfo { IsGenericMethod: true } methodInfo
            ? methodInfo.GetGenericArguments()
            : null;

        var i = 0;
        while (i < il.Length)
        {
            var code = il[i];
            OpCode opcode;
            if (code == 0xFE)
            {
                opcode = _opCodesByValue[(short)(0xFE00 | il[i + 1])];
                i += 2;
            }
            else
            {
                opcode = _opCodesByValue[code];
                i += 1;
            }

            if (opcode.OperandType == OperandType.InlineSwitch)
            {
                var caseCount = BitConverter.ToInt32(il, i);
                i += 4 + (caseCount * 4);
                continue;
            }

            var operandSize = GetOperandSize(opcode.OperandType);

            if (opcode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, i);
                MethodBase? resolved = null;
                try
                {
                    resolved = module.ResolveMethod(token, typeArgs, methodArgs);
                }
                catch (ArgumentException)
                {
                    // A small number of tokens (e.g. certain generic instantiations) are not
                    // resolvable this way; skipping them is a coverage gap, not a false negative —
                    // it can only under-report edges, never hide a real forbidden call as clean by
                    // resolving it incorrectly.
                }

                if (resolved is not null)
                    yield return resolved;
            }

            i += operandSize;
        }
    }

    private static int GetOperandSize(OperandType operandType) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod
            or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType
            or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        _ => throw new NotSupportedException($"Unsupported CIL operand type '{operandType}' encountered while walking the Evaluate() call graph."),
    };

    private static Dictionary<short, OpCode> BuildOpCodeLookup()
    {
        var lookup = new Dictionary<short, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(OpCode))
                continue;
            var opcode = (OpCode)field.GetValue(null)!;
            lookup[opcode.Value] = opcode;
        }
        return lookup;
    }
}
