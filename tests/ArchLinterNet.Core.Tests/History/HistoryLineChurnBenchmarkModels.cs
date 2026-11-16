namespace ArchLinterNet.Core.Tests.History;

internal sealed record LineChurnBaselineMeasurement(
    string WorkloadId,
    string Family,
    string InputSha256,
    string ExpectedOutputSha256,
    int LineCount,
    int EditCount,
    int ReducedOldLineCount,
    int ReducedNewLineCount,
    long BaselineDpCellUpdates,
    long Additions,
    long Deletions,
    string OutputDigest,
    IReadOnlyList<double> ElapsedMillisecondsSamples,
    IReadOnlyList<long> AllocatedBytesSamples,
    double MedianMilliseconds,
    long MedianAllocatedBytes);

internal sealed record HistoryIngestionBaselineMeasurement(
    string WorkloadId,
    double DirectCalculatorMedianMilliseconds,
    IReadOnlyList<double> FullIngestionCallSamplesMilliseconds,
    double FullIngestionCallMedianMilliseconds,
    IReadOnlyList<double> IngestionPhaseSamplesMilliseconds,
    double IngestionPhaseMedianMilliseconds,
    double? DirectToIngestionShare,
    double? EndToEndAmdahlUpperBound);

internal sealed record LineChurnCurrentMeasurement(
    string WorkloadId,
    LineChurnBaselineMeasurement Direct,
    LineChurnWorkCounts Work,
    long FastPathWork,
    long TotalAlgorithmWork,
    long BaselineDpCellUpdates,
    double DirectTimeRatio,
    double AllocationRatio,
    long FrontierBytes,
    long DynamicProgrammingRowBytes);

internal sealed record LineChurnWorkCounts(
    bool UsedMyers,
    bool UsedFallback,
    long FrontierSteps,
    long LineComparisons,
    long DynamicProgrammingCells,
    int FrontierArrayLength,
    int DynamicProgrammingRowLength);
