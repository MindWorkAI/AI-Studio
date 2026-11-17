namespace AIStudio.Tools.PluginSystem;

public readonly record struct InternalPluginData(PluginType Type, Guid Id, string ShortName)
{
    public string ResourcePath => $"{this.Type.Directory}/{this.ShortName.ToLowerInvariant()}-{this.Id}";
    
    public string ResourceName => $"{this.ShortName.ToLowerInvariant()}-{this.Id}";
}