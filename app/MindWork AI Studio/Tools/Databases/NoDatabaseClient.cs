using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Databases;

public sealed class NoEmbeddingStore(string name, string? unavailableReason, DatabaseClientStatus status = DatabaseClientStatus.UNAVAILABLE) : EmbeddingStore(name, string.Empty)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(NoEmbeddingStore).Namespace, nameof(NoEmbeddingStore));
    
    public override DatabaseClientStatus Status => status;
    
    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        yield return (TB("Status"), TB("Unavailable"));

        if (!string.IsNullOrWhiteSpace(unavailableReason))
            yield return (TB("Reason"), unavailableReason);

        await Task.CompletedTask;
    }
    public override void Dispose()
    {
    }
}
