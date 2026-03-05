namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class PropSpec(IEnumerable<string> required, IEnumerable<string> optional)
{
    public IReadOnlyList<string> Required { get; } = required.ToArray();
    public IReadOnlyList<string> Optional { get; } = optional.ToArray();
}