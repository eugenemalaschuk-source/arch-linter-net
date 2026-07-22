namespace ArchLinterNet.Core.Discovery.Abstractions;

internal interface IArchitectureFrameworkReferenceEvaluator
{
    ArchitectureFrameworkReferenceEvaluationResult Evaluate(string projectAbsolutePath);
}
