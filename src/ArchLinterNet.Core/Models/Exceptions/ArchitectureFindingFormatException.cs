namespace ArchLinterNet.Core.Model;

public sealed class ArchitectureFindingFormatException(string message) : InvalidOperationException(message);
