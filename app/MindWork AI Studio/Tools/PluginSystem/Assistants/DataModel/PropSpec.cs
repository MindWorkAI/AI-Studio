using System.Collections.Immutable;

using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class PropSpec(
    IEnumerable<string> required,
    IEnumerable<string> optional,
    IEnumerable<string>? nonReadable = null,
    IEnumerable<string>? nonWriteable = null,
    IEnumerable<string>? confidential = null,
    IReadOnlyDictionary<string, string>? formerNames = null)
{
    public ImmutableArray<string> Required { get; } = MaterializeDistinct(required);
    public ImmutableArray<string> Optional { get; } = MaterializeDistinct(optional);
    public ImmutableArray<string> Confidential { get; } = MaterializeDistinct(confidential ?? []);
    public ImmutableArray<string> NonReadable { get; } = MaterializeDistinct((nonReadable ?? []).Concat(confidential ?? []));
    public ImmutableArray<string> NonWriteable { get; } = MaterializeDistinct((nonWriteable ?? []).Concat(confidential ?? []));

    /// <summary>
    /// The current name of each prop which had another name before, by that former name.
    /// </summary>
    /// <remarks>
    /// Plugins keep using a former name for good, also in the props their callbacks return: a plugin
    /// which changes it needs a new security audit. When a plugin uses both names, the current one
    /// wins. Callbacks see a prop under its current name only.
    /// </remarks>
    public ImmutableDictionary<string, string> FormerNames { get; } = (formerNames ?? ImmutableDictionary<string, string>.Empty).ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Reads a prop from the props of a component, under its current name or under a former name
    /// which the plugin still uses.
    /// </summary>
    /// <param name="propsTable">The props of the component, as the plugin defines them.</param>
    /// <param name="name">The current name of the prop.</param>
    /// <param name="value">The value of the prop, when the plugin defines it.</param>
    /// <returns>True, when the plugin defines the prop.</returns>
    public bool TryGetValue(LuaTable propsTable, string name, out LuaValue value)
    {
        if (propsTable.TryGetValue(name, out value))
            return true;

        foreach (var (formerName, currentName) in this.FormerNames)
            if (string.Equals(currentName, name, StringComparison.Ordinal) && propsTable.TryGetValue(formerName, out value))
                return true;

        return false;
    }

    /// <summary>
    /// Determines the current name of a prop which a plugin names in its props, e.g., in an update
    /// a callback returns.
    /// </summary>
    /// <param name="name">The name the plugin uses.</param>
    /// <param name="propsTable">The props in which the plugin uses the name.</param>
    /// <param name="currentName">The current name of the prop. It is the given name, unless that is a former one.</param>
    /// <returns>False, when the plugin uses a former name next to the current one, which wins.</returns>
    public bool TryGetCurrentName(string name, LuaTable propsTable, out string currentName)
    {
        if (!this.FormerNames.TryGetValue(name, out var nameAfterRenaming))
        {
            currentName = name;
            return true;
        }

        currentName = nameAfterRenaming;
        return !propsTable.TryGetValue(currentName, out _);
    }

    private static ImmutableArray<string> MaterializeDistinct(IEnumerable<string> source)
    {
        return source.Distinct(StringComparer.Ordinal).ToImmutableArray();
    }
}
