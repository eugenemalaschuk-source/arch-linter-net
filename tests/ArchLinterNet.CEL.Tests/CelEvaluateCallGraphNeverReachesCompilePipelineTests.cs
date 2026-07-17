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
/// the token-resolved declaration — one mechanism for class-hierarchy overrides
/// (<see cref="MethodInfo.GetBaseDefinition"/>) and a separate one for interface implementations,
/// implicit or explicit (<see cref="Type.GetInterfaceMap"/>), since <c>GetBaseDefinition()</c>
/// does not connect an interface declaration to any implementing method.
/// <see cref="CelInterfaceDispatchClosureSanityCheckTests"/> proves both branches actually find
/// their targets, using a synthetic interface/implementations pair scoped to the test assembly.
/// </para>
/// <para>
/// It also accounts for indirect dispatch the token-based walk cannot see through directly:
/// <c>calli</c> (an unmanaged/managed function-pointer call with no method token at all — never
/// expected in this codebase, which contains no unsafe code or function pointers; if one is ever
/// encountered, it is fail-closed reported as unresolved rather than silently skipped) and delegate
/// invocation (<c>callvirt</c> to <c>Invoke</c>/<c>BeginInvoke</c>/<c>EndInvoke</c> on any
/// <see cref="Delegate"/>-derived type — the IL at the invocation site alone never names the actual
/// wrapped method). For delegate invocation, <see cref="ResolveDelegateInvocationTargets"/> answers
/// "what could this delegate actually point to?" by scanning every method in the assembly for
/// <c>ldftn</c>/<c>ldvirtftn</c> instructions (the only two ways a non-capturing delegate can be
/// constructed in this codebase — the actual pattern <c>CelEvaluator</c> uses for its
/// <c>Func&lt;int, bool&gt;</c>/<c>Func&lt;bool, bool&gt;</c> comparison/projection delegates) and
/// matching by signature against the delegate's own <c>Invoke</c> signature. This is a sound
/// over-approximation, not a precise per-call-site resolution: it may include a method that isn't
/// actually reachable as *this specific* delegate's target if some other, unrelated
/// same-signature method also happens to be captured elsewhere in the assembly — acceptable, since
/// over-exploring cannot hide a forbidden edge the way under-exploring could. If no matching
/// construction site is found anywhere in the assembly (e.g. the delegate could only have come
/// from a field, a closure, or outside the assembly), that invocation is fail-closed reported as
/// unresolved rather than silently treated as a dead end — the previous version of this walker did
/// exactly that for every delegate invocation, since <c>Invoke()</c> has no method body of its own
/// and the BCL delegate type is filtered out by the assembly boundary before <em>and</em> after
/// this fix; what changed is that a construction-site match now short-circuits the "unresolved"
/// report instead of the walker silently doing nothing either way.
/// <see cref="CelDelegateDispatchClosureSanityCheckTests"/> proves the resolution actually finds a
/// non-capturing lambda's target using a synthetic method scoped to the test assembly.
/// </para>
/// </remarks>
[TestFixture]
public sealed class CelEvaluateCallGraphNeverReachesCompilePipelineTests
{
    private static readonly Type[] _forbiddenCompilePipelineTypes = [typeof(CelTokenizer), typeof(CelParser), typeof(CelBinder)];
    private static readonly Assembly _celAssembly = typeof(CelCompiledPredicate).Assembly;
    private static readonly Dictionary<short, OpCode> _opCodesByValue = BuildOpCodeLookup();
    private static readonly Dictionary<MethodInfo, IReadOnlyList<MethodBase>> _virtualOverrideCache = [];
    private static readonly Dictionary<Assembly, ILookup<string, MethodInfo>> _ldftnTargetIndexCache = [];

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
        foreach (var (opcode, token, resolved, failure) in WalkInlineMethodInstructions(method))
        {
            if (opcode.Value == OpCodes.Calli.Value)
            {
                // Fail-closed: calli has no method token at all — a function-pointer call whose
                // target cannot be determined from the IL by any means this walker has. Never
                // expected in this codebase (no unsafe code/function pointers); if this ever fires,
                // it must be investigated by hand, not silently skipped.
                unresolvedEdges.Add($"{method.DeclaringType}.{method.Name} -> calli (unmanaged/managed function pointer call, no method token)");
                continue;
            }

            if (failure is not null)
            {
                // Fail-closed, not fail-open: an edge this walker cannot resolve is recorded as
                // a failure the caller must surface, rather than silently dropped — a dropped
                // edge could just as easily be the one call into a forbidden pipeline type.
                unresolvedEdges.Add($"{method.DeclaringType}.{method.Name} -> token 0x{token:X8} ({failure.Message})");
                continue;
            }

            if (resolved is null)
                continue;

            if (IsDelegateInvocation(resolved))
            {
                var targets = ResolveDelegateInvocationTargets((MethodInfo)resolved, _celAssembly);
                if (targets.Count == 0)
                {
                    unresolvedEdges.Add(
                        $"{method.DeclaringType}.{method.Name} -> {resolved.DeclaringType}.{resolved.Name} " +
                        "(delegate invocation; no matching ldftn/ldvirtftn construction site found anywhere " +
                        "in the scanned assembly — target cannot be determined statically)");
                    continue;
                }

                foreach (var target in targets)
                    yield return target;
                continue;
            }

            // callvirt: the token names only the statically-declared method; the actual
            // runtime target could be any override. call/newobj/ldftn/ldvirtftn have no such
            // ambiguity for non-delegate targets — the token already names the exact target.
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

    private static bool IsDelegateInvocation(MethodBase method) =>
        typeof(Delegate).IsAssignableFrom(method.DeclaringType)
        && method.Name is "Invoke" or "BeginInvoke" or "EndInvoke";

    /// <summary>
    /// Sound over-approximation of a delegate invocation's possible targets: every method anywhere
    /// in <paramref name="scanAssembly"/> that is the target of an <c>ldftn</c>/<c>ldvirtftn</c>
    /// instruction and whose signature (parameter types, in order, plus return type) matches
    /// <paramref name="delegateInvokeMethod"/>'s own signature. See the type-level remarks for why
    /// this is sound (may over-include, cannot under-include) rather than a precise per-call-site
    /// resolution.
    /// </summary>
    internal static IReadOnlyList<MethodInfo> ResolveDelegateInvocationTargets(MethodInfo delegateInvokeMethod, Assembly scanAssembly)
    {
        var index = BuildLdftnTargetIndex(scanAssembly);
        return index[SignatureKey(delegateInvokeMethod)].ToList();
    }

    private static ILookup<string, MethodInfo> BuildLdftnTargetIndex(Assembly scanAssembly)
    {
        if (_ldftnTargetIndexCache.TryGetValue(scanAssembly, out var cached))
            return cached;

        var found = new List<MethodInfo>();
        foreach (var type in scanAssembly.GetTypes())
        {
            var members = type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Cast<MethodBase>()
                .Concat(type.GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly));

            foreach (var member in members)
            {
                foreach (var (opcode, _, resolved, _) in WalkInlineMethodInstructions(member))
                {
                    if ((opcode.Value == OpCodes.Ldftn.Value || opcode.Value == OpCodes.Ldvirtftn.Value) && resolved is MethodInfo methodInfo)
                        found.Add(methodInfo);
                }
            }
        }

        var index = found.Distinct().ToLookup(SignatureKey);
        _ldftnTargetIndexCache[scanAssembly] = index;
        return index;
    }

    private static string SignatureKey(MethodInfo method) =>
        method.ReturnType.FullName + "(" + string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName)) + ")";

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

    /// <summary>
    /// Low-level shared IL walker: yields one entry per instruction in <paramref name="method"/>
    /// whose operand is a method token (<c>call</c>, <c>callvirt</c>, <c>newobj</c>, <c>ldftn</c>,
    /// <c>ldvirtftn</c>, <c>jmp</c>) or is <c>calli</c> (no token, <c>InlineSig</c> operand instead).
    /// For a method-token instruction, <c>resolved</c>/<c>failure</c> report the
    /// <see cref="Module.ResolveMethod(int, Type[], Type[])"/> outcome; for <c>calli</c> both are
    /// <c>null</c> and the caller must recognize it by <c>opcode</c> alone (see
    /// <see cref="GetCalledMethods"/>). Shared by both the call-graph walk itself and
    /// <see cref="BuildLdftnTargetIndex"/>'s assembly-wide <c>ldftn</c>/<c>ldvirtftn</c> pre-scan,
    /// so the two cannot silently diverge on IL-decoding details.
    /// </summary>
    internal static IEnumerable<(OpCode Opcode, int Token, MethodBase? Resolved, Exception? Failure)> WalkInlineMethodInstructions(MethodBase method)
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

            if (opcode.Value == OpCodes.Calli.Value)
            {
                yield return (opcode, 0, null, null);
            }
            else if (opcode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, i);
                MethodBase? resolved = null;
                Exception? failure = null;
                try
                {
                    resolved = module.ResolveMethod(token, typeArgs, methodArgs);
                }
                catch (ArgumentException ex)
                {
                    failure = ex;
                }

                yield return (opcode, token, resolved, failure);
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
