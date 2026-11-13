namespace AIStudio.Tools.Databases.VectorStore;

/// <summary>
/// What a backup needs to copy the stores, one at a time.
/// </summary>
/// <param name="BackupId">Names the backup when its stores are copied.</param>
/// <param name="StagingPath">The directory of the backup's copies. Below it, every copy has the place it has below the data directory. The backup removes it once it is written.</param>
/// <param name="Stores">Every store which was created completely.</param>
public sealed record VectorStoreBackupPreparation(string BackupId, string StagingPath, IReadOnlyList<VectorStoreBackupStore> Stores);