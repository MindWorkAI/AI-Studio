using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Databases;

public sealed class NoDatabaseClient(string name, string? unavailableReason) : DatabaseClient(name, string.Empty)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(NoDatabaseClient).Namespace, nameof(NoDatabaseClient));
    
    public override bool IsAvailable => false;

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