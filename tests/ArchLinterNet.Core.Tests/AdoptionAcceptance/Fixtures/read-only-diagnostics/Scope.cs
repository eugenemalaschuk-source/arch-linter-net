using System;
using Microsoft.Extensions.DependencyInjection;

namespace Repro.Consumer;

/// <summary>Exercises a public property whose signature is supplied by DI Abstractions 10.</summary>
public sealed class ScopeProperty
{
    public IServiceScope Scope { get; }

    public ScopeProperty(IServiceScope scope) => Scope = scope;
}

/// <summary>Exercises the direct IServiceScope interface implementation variant.</summary>
public sealed class Scope : IServiceScope
{
    public IServiceProvider ServiceProvider => throw new NotSupportedException();

    public void Dispose()
    {
    }
}
