namespace AIStudio.Tools.Databases.VectorStore;

/// <summary>
/// A store which a backup takes along.
/// </summary>
/// <param name="StoreName">The name of the store.</param>
/// <param name="Bytes">How large the store is on the disk.</param>
public sealed record VectorStoreBackupStore(string StoreName, long Bytes);