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
/// reachable from every public <c>Evaluate</c> overload on <see cref="CelCompiledPredicate"/> and
/// <see cref="CelCompiledExpression"/> — including the safe-default
/// <see cref="CelCompiledPredicate.Evaluate(CelEvaluationContext)"/> overload, not only the
/// explicit-limits one — and asserts the walk never reaches <see cref="CelTokenizer"/>,
/// <see cref="CelParser"/>, or <see cref="CelBinder"/>. Unlike a call counter sampled over one run,
/// this covers every code path in the current build unconditionally — including paths a runtime
/// sample would miss — and unlike the parameter-signature check in
/// <c>CelRepeatedEvaluationNoReparseTests</c>, it also rules out a regression that
/// re-tokenizes/re-parses/re-binds using state already held on the compiled instance (e.g.
/// <c>CompilationKey.NormalizedSource</c>) rather than a new external input.
/// </summary>
/// <remarks>
/// <para>
/// The walk is fail-closed on unresolved method tokens (see <see cref="GetCalledMethods"/>): an
/// edge this walker cannot resolve is reported as a failure rather than silently dropped, since a
/// silently-dropped edge could just as easily be the one edge into the forbidden pipeline types.
/// </para>
/// <para>
/// It is also conservative for virtual dispatch: for a <c>callvirt</c> instruction, the token
/// resolves only to the statically-declared method — the actual runtime target could be any
/// override, and the IL alone does not say which. <see cref="ResolveVirtualTargets"/> follows
/// every override of the resolved virtual method found anywhere in the scanned assembly, not just
/// the token-resolved declaration. This covers two distinct dispatch mechanisms, handled
/// separately because <see cref="MethodInfo.GetBaseDefinition"/> only answers one of them: for a
/// class-hierarchy virtual method it walks up to the root declaration and matches every override
/// sharing that root; for an <em>interface</em> method — where <c>GetBaseDefinition()</c> does not
/// connect the interface declaration to any implementing class's method, implicit or explicit —
/// it uses <see cref="Type.GetInterfaceMap"/> against every type in the assembly that implements
/// the interface, which is the API that actually answers "which method does this type use to
/// satisfy this interface member." Without both, a <c>callvirt</c> through an interface or a
/// base-typed reference could hide an edge into
/// <see cref="CelTokenizer"/>/<see cref="CelParser"/>/<see cref="CelBinder"/> by resolving to an
/// unrelated declaration with no body. A non-virtual <c>call</c> has no such ambiguity — the token
/// is the exact target — so this treatment applies only to <c>callvirt</c>.
/// <see cref="CelInterfaceDispatchClosureSanityCheckTests"/> proves both branches actually find
/// their targets, using a synthetic interface/implementations pair scoped to the test assembly.
/// </para>
/// </remarks>
[TestFixture]
public sealed class CelEvaluateCallGraphNeverReachesCompilePipelineTests
{
    private static readonly Type[] _forbiddenCompilePipelineTypes = [typeof(CelTokenizer), typeof(CelParser), typeof(CelBinder)];
    private static readonly Assembly _celAssembly = typeof(CelCompiledPredicate).Assembly;
    private static readonly Dictionary<short, OpCode> _opCodesByValue = BuildOpCodeLookup();
    private static readonly Dictionary<MethodInfo, IReadOnlyList<MethodBase>> _virtualOverrideCache = [];

    [Test]
    public void Evaluate_CallGraph_NeverReachesTokenizerParserOrBinder()
    {
        var roots = new[]
        {
            typeof(CelCompiledPredicate).GetMethod(
                nameof(CelCompiledPredicate.Evaluate), [typeof(CelEvaluationContext), typeof(CelEvaluationLimits)]),
            typeof(CelCompiledPredicate).GetMethod(
                nameof(CelCompiledPredicate.Evaluate), [typeof(CelEvaluationContext)]),
            typeof(CelCompiledExpression).GetMethod(
                nameof(CelCompiledExpression.Evaluate), [typeof(CelEvaluationContext), typeof(CelEvaluationLimits)]),
            typeof(CelCompiledExpression).GetMethod(
                nameof(CelCompiledExpression.Evaluate), [typeof(CelEvaluationContext)]),
        };
        Assert.That(roots, Has.All.Not.Null,
            "Both the explicit-limits and the safe-default Evaluate(context) entry points must exist on both compiled types.");

        var visited = new HashSet<MethodBase>();
        var offenders = new List<MethodBase>();
        var unresolvedEdges = new List<string>();
        var stack = new Stack<MethodBase>(roots!);

        // Visited-set cap: a genuine explosion here (thousands of distinct CEL-assembly methods
        // reachable from the four Evaluate() overloads) would itself be surprising enough to
        // investigate rather than silently walk forever — see the "no silent caps" note this
        // throws if hit.
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

            foreach (var called in GetCalledMethods(method, unresolvedEdges))
            {
                // Only follow edges within ArchLinterNet.CEL itself — BCL/runtime methods are not
                // part of the compile pipeline and following them would make the graph unbounded.
                if (called.Module.Assembly == _celAssembly && !visited.Contains(called))
                    stack.Push(called);
            }
        }

        // Fail-closed: an edge this walker could not resolve is treated as a potential hidden call
        // into the forbidden pipeline, not silently ignored — the offenders check below cannot see
        // past an edge it never followed.
        Assert.That(unresolvedEdges, Is.Empty,
            "Call-graph walk hit unresolvable method token(s) — this test cannot certify the " +
            "no-reparse guarantee while an edge is unaccounted for. Investigate and either resolve " +
            "them or narrow the walker; do not silently ignore. Unresolved: " +
            string.Join("; ", unresolvedEdges));

        Assert.That(offenders, Is.Empty,
            "Evaluate()'s call graph must never reach CelTokenizer/CelParser/CelBinder. Reached: " +
            string.Join(", ", offenders.Select(m => $"{m.DeclaringType}.{m.Name}")));
    }

    private static IEnumerable<MethodBase> GetCalledMethods(MethodBase method, List<string> unresolvedEdges)
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
                catch (ArgumentException ex)
                {
                    // Fail-closed, not fail-open: an edge this walker cannot resolve is recorded as
                    // a failure the caller must surface, rather than silently dropped — a dropped
                    // edge could just as easily be the one call into a forbidden pipeline type.
                    unresolvedEdges.Add($"{method.DeclaringType}.{method.Name} -> token 0x{token:X8} ({ex.Message})");
                }

                if (resolved is not null)
                {
                    // callvirt: the token names only the statically-declared method; the actual
                    // runtime target could be any override. call/newobj/ldftn have no such
                    // ambiguity — the token already names the exact target.
                    if (opcode.Value == OpCodes.Callvirt.Value && resolved is MethodInfo { IsVirtual: true } virtualMethod)
                    {
                        foreach (var target in ResolveVirtualTargets(virtualMethod, _celAssembly, unresolvedEdges))
                            yield return target;
                    }
                    else
                    {
                        yield return resolved;
                    }
                }
            }

            i += operandSize;
        }
    }

    /// <summary>
    /// Returns the token-resolved virtual method itself, plus every override of it (class-hierarchy
    /// override, or interface implementation — implicit or explicit) declared anywhere in
    /// <paramref name="scanAssembly"/> — conservative handling for virtual dispatch/interface calls,
    /// since a <c>callvirt</c> token alone does not identify which override actually runs. A type
    /// that implements the interface but whose implementation this method fails to resolve (see the
    /// <see cref="Type.GetInterfaceMap"/> failure case below) is recorded into
    /// <paramref name="unresolvedEdges"/> rather than silently skipped — fail-closed, matching
    /// <see cref="GetCalledMethods"/>'s own token-resolution failures: a type whose implementation
    /// could not be checked is exactly as unaccounted-for as an edge that could not be resolved at
    /// all, and could just as easily be the one implementation that calls into the forbidden
    /// pipeline. Results are cached per resolved method; a given assembly's type set does not
    /// change during a test run — the cache is safe across repeated calls for the same method
    /// because any failure it recorded was already appended to the caller's
    /// <paramref name="unresolvedEdges"/> list on the first (uncached) call. Internal (not
    /// <c>private</c>) so <c>CelInterfaceDispatchClosureSanityCheckTests</c> can exercise it
    /// directly against a synthetic assembly.
    /// </summary>
    internal static IReadOnlyList<MethodBase> ResolveVirtualTargets(
        MethodInfo virtualMethod, Assembly scanAssembly, List<string> unresolvedEdges)
    {
        if (_virtualOverrideCache.TryGetValue(virtualMethod, out var cached))
            return cached;

        var targets = new List<MethodBase> { virtualMethod };

        if (virtualMethod.DeclaringType is { IsInterface: true } interfaceType)
        {
            // GetBaseDefinition() does not connect an interface method declaration to any
            // implementing class's method — Type.GetInterfaceMap is the API that actually maps an
            // interface member to the concrete method a given type uses to satisfy it, covering
            // both implicit and explicit interface implementations.
            foreach (var type in scanAssembly.GetTypes())
            {
                if (type.IsInterface || !interfaceType.IsAssignableFrom(type))
                    continue;

                InterfaceMapping mapping;
                try
                {
                    mapping = type.GetInterfaceMap(interfaceType);
                }
                catch (ArgumentException ex)
                {
                    // Fail-closed: a type this method could not map is a potential hidden
                    // implementation the walk never gets to inspect — recorded as an unresolved
                    // edge so Evaluate_CallGraph_NeverReachesTokenizerParserOrBinder's own
                    // unresolvedEdges assertion fails the test, exactly like a raw IL token-
                    // resolution failure in GetCalledMethods does.
                    unresolvedEdges.Add(
                        $"{virtualMethod.DeclaringType}.{virtualMethod.Name} -> could not map onto " +
                        $"implementing type {type} ({ex.Message})");
                    continue;
                }

                for (var idx = 0; idx < mapping.InterfaceMethods.Length; idx++)
                {
                    if (mapping.InterfaceMethods[idx] == virtualMethod)
                        targets.Add(mapping.TargetMethods[idx]);
                }
            }
        }
        else
        {
            var baseDefinition = virtualMethod.GetBaseDefinition();
            foreach (var type in scanAssembly.GetTypes())
            {
                foreach (var candidate in type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (candidate.IsVirtual && !Equals(candidate, virtualMethod) && candidate.GetBaseDefinition() == baseDefinition)
                        targets.Add(candidate);
                }
            }
        }

        _virtualOverrideCache[virtualMethod] = targets;
        return targets;
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
