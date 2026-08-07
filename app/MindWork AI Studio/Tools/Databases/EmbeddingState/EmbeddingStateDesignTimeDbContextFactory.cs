using Microsoft.EntityFrameworkCore.Design;

namespace AIStudio.Tools.Databases.EmbeddingState;

internal sealed class EmbeddingStateDesignTimeDbContextFactory : IDesignTimeDbContextFactory<EmbeddingStateDbContext>
{
    public EmbeddingStateDbContext CreateDbContext(string[] args)
    {
        var databasePath = args.FirstOrDefault(argument => argument.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(databasePath))
            databasePath = Path.Combine(Path.GetTempPath(), "mindwork-ai-studio-rag-index-design.sqlite3");

        return new EmbeddingStateDbContext(EmbeddingStateDbContext.CreateOptions(databasePath));
    }
}
