namespace AIStudio.Tools.PluginSystem;

public sealed record NoConfigurationObject : ConfigurationBaseObject
{
    public static readonly NoConfigurationObject INSTANCE = new();
    
    private NoConfigurationObject()
    {
        this.Id = Guid.Empty.ToString();
        this.Name = "No Configuration";
    }

    #region Overrides of ConfigurationBaseObject

    public override string Id { get; init; }
    
    public override uint Num { get; init; }
    
    public override string Name { get; init; }
    
    public override bool IsEnterpriseConfiguration { get; init; }
    
    public override Guid EnterpriseConfigurationPluginId { get; init; }

    #endregion
}