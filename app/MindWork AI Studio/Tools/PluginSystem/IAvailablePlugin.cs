namespace AIStudio.Tools.PluginSystem;

public interface IAvailablePlugin : IPluginMetadata
{
    public string LocalPath { get; }
}