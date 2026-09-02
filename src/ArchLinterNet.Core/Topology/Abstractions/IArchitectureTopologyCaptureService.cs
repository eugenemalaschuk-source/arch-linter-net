namespace ArchLinterNet.Core.Topology.Abstractions;

/// <summary>Captures canonical, read-only topology observations from one Core analysis session.</summary>
internal interface IArchitectureTopologyCaptureService
{
    ArchitectureTopologyCaptureOutcome Capture(ArchitectureTopologyCaptureRequest request);
}
