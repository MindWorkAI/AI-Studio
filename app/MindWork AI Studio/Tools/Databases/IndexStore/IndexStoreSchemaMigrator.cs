using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.IndexStore;

internal static class IndexStoreSchemaMigrator
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.InitialRagIndex))]
    public static async Task MigrateAsync(IndexStoreDbContext context, CancellationToken token)
    {
        await context.Database.MigrateAsync(token);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", token);
    }
}
