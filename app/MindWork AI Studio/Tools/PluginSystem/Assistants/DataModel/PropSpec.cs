using System.Collections.Immutable;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class PropSpec(
    IEnumerable<string> required,
    IEnumerable<string> optional,
    IEnumerable<string>? nonReadable = null,
    IEnumerable<string>? nonWriteable = null,
    IEnumerable<string>? confidential = null)
{
    public ImmutableArray<string> Required { get; } = MaterializeDistinct(required);
    public ImmutableArray<string> Optional { get; } = MaterializeDistinct(optional);
    public ImmutableArray<string> Confidential { get; } = MaterializeDistinct(confidential ?? []);
    public ImmutableArray<string> NonReadable { get; } = MaterializeDistinct((nonReadable ?? []).Concat(confidential ?? []));
    public ImmutableArray<string> NonWriteable { get; } = MaterializeDistinct((nonWriteable ?? []).Concat(confidential ?? []));

    private static ImmutableArray<string> MaterializeDistinct(IEnumerable<string> source)
    {
        return source.Distinct(StringComparer.Ordinal).ToImmutableArray();
    }
}
