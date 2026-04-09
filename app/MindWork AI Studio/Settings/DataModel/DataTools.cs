namespace AIStudio.Settings.DataModel;

public sealed class DataTools
{
    public Dictionary<string, Dictionary<string, string>> Settings { get; set; } = [];

    public Dictionary<string, HashSet<string>> DefaultToolIdsByComponent { get; set; } = [];

    public HashSet<string> VisibleToolSelectionComponents { get; set; } = [];
}
