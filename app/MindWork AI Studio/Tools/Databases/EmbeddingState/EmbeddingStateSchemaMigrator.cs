using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.EmbeddingState;

internal static class EmbeddingStateSchemaMigrator
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Migrations.InitialRagIndex))]
    public static async Task MigrateAsync(EmbeddingStateDbContext context, CancellationToken token)
    {
        await context.Database.MigrateAsync(token);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", token);
    }
}
