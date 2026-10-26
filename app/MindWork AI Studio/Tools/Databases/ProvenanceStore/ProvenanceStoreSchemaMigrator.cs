using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.ProvenanceStore;

internal static class ProvenanceStoreSchemaMigrator
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.InitialProvenanceStore))]
    public static async Task MigrateAsync(ProvenanceStoreDbContext context, CancellationToken token)
    {
        await context.Database.MigrateAsync(token);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", token);
    }
}