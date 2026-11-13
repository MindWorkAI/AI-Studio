namespace AIStudio.Tools.Databases.VectorStore;

/// <summary>
/// The vector of one point, as the vector store holds it.
/// </summary>
/// <param name="PointId">The id of the point, which is also the id of its chunk.</param>
/// <param name="Vector">The vector.</param>
public sealed record StoredVector(string PointId, IReadOnlyList<float> Vector);