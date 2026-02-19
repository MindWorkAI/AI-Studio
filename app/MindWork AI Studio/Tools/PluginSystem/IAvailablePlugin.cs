namespace AIStudio.Tools.PluginSystem;

public interface IAvailablePlugin : IPluginMetadata
{
    public string LocalPath { get; }
    
    public bool IsManagedByConfigServer { get; }
    
    public Guid? ManagedConfigurationId { get; }
}