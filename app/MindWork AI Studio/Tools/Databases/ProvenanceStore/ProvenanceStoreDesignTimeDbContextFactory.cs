using Microsoft.EntityFrameworkCore.Design;

namespace AIStudio.Tools.Databases.ProvenanceStore;

internal sealed class ProvenanceStoreDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProvenanceStoreDbContext>
{
    public ProvenanceStoreDbContext CreateDbContext(string[] args)
    {
        var databasePath = args.FirstOrDefault(argument => argument.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(databasePath))
            databasePath = Path.Combine(Path.GetTempPath(), "mindwork-ai-studio-provenance-design.sqlite3");

        return new ProvenanceStoreDbContext(ProvenanceStoreDbContext.CreateOptions(databasePath));
    }
}