namespace ArchLinterNet.CEL;

/// <summary>
/// Indicates the severity of a <see cref="CelDiagnostic"/>.
/// </summary>
public enum CelDiagnosticSeverity
{
    /// <summary>A fatal error that prevents compilation or evaluation from succeeding.</summary>
    Error,

    /// <summary>A non-fatal issue that does not block compilation or evaluation.</summary>
    Warning,

    /// <summary>Informational; does not indicate a problem.</summary>
    Info,
}
