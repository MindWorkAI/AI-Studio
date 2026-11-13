namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// A tool collection written in C#: what it is, and how the app presents it.
/// </summary>
/// <remarks>
/// Like a tool written in C#, a collection states its own definition, so no string key has to join
/// a definition to its texts. Whether a collection exists right now is not its own question: it
/// exists as long as one of its tools does, see IToolImplementation.IsAvailable.
/// </remarks>
public interface IToolCollection
{
    public ToolCollectionDefinition GetDefinition();

    public string Icon { get; }

    public string GetDisplayName();

    public string GetDescription();
}