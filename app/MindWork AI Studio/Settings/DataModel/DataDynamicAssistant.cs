namespace AIStudio.Settings.DataModel;

public sealed class DataDynamicAssistant
{
    public Dictionary<string, List<string>> PersistentFileAttachments { get; set; } = new(StringComparer.Ordinal);
}
