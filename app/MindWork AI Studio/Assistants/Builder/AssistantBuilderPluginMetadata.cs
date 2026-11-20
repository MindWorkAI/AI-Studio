namespace AIStudio.Assistants.Builder;

internal sealed class AssistantBuilderPluginMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Categories { get; set; } = [];

    /// <summary>
    /// The names of the custom categories, when the categories contain CUSTOM.
    /// </summary>
    public string[] CustomCategories { get; set; } = [];
}