using Microsoft.EntityFrameworkCore.Design;

namespace AIStudio.Tools.Databases.IndexStore;

internal sealed class IndexStoreDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IndexStoreDbContext>
{
    public IndexStoreDbContext CreateDbContext(string[] args)
    {
        var databasePath = args.FirstOrDefault(argument => argument.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(databasePath))
            databasePath = Path.Combine(Path.GetTempPath(), "mindwork-ai-studio-rag-index-design.sqlite3");

        return new IndexStoreDbContext(IndexStoreDbContext.CreateOptions(databasePath));
    }
}
