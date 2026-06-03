namespace AIStudio.Tools.Rust;

/// <summary>
/// The response of the Qdrant Edge information request.
/// </summary>
public readonly record struct QdrantEdgeInfo
{
    public QdrantEdgeStatus Status { get; init; }

    public bool IsAvailable { get; init; }

    public string? UnavailableReason { get; init; }

    public string Name { get; init; }

    public string Version { get; init; }

    public string Path { get; init; }

    public int StoresCount { get; init; }

    public static QdrantEdgeInfo Unavailable(string reason) => new()
    {
        Status = QdrantEdgeStatus.UNAVAILABLE,
        UnavailableReason = reason
    };
}
