using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.IndexStore;

internal static class IndexStoreSchemaMigrator
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.InitialRagIndex))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.PermanentIndexingFailures))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.DropFileConfidenceLevel))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.DropDataSourceName))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.MailboxIndex))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.MediaTranscriptKey))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.ChunkEndPageNumber))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.ChunkTextHash))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.ChunkGeneration))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.IndexedFileContentHash))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.IndexedFileGeneration))]
    public static async Task MigrateAsync(IndexStoreDbContext context, CancellationToken token)
    {
        await context.Database.MigrateAsync(token);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", token);
    }
}