using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class DataSourceSecurityExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceSecurityExtensions).Namespace, nameof(DataSourceSecurityExtensions));
    
    public static string ToSelectionText(this DataSourceSecurity security) => security switch
    {
        DataSourceSecurity.NOT_SPECIFIED => TB("Please select a security policy"),
        
        DataSourceSecurity.ALLOW_ANY => TB("This data source can be used with any LLM provider. Your data may be sent to a cloud-based provider."),
        DataSourceSecurity.SELF_HOSTED => TB("This data source can only be used with a self-hosted LLM provider. Your data will not be sent to any cloud-based provider."),
        
        _ => TB("Unknown security policy")
    };
    
    public static string ToInfoText(this DataSourceSecurity security) => security switch
    {
        DataSourceSecurity.NOT_SPECIFIED => TB("The security of the data source is not specified yet. You cannot use this data source until you specify a security policy."),
        
        DataSourceSecurity.ALLOW_ANY => TB("This data source can be used with any LLM provider. Your data may be sent to a cloud-based provider."),
        DataSourceSecurity.SELF_HOSTED => TB("This data source can only be used with a self-hosted LLM provider. Your data will not be sent to any cloud-based provider."),
        
        _ => TB("Unknown security policy")
    };
    
    /// <summary>
    /// The data security which demands what the one held and the one arriving demand together.
    /// </summary>
    /// <remarks>
    /// Data which may only be used with self-hosted providers keeps them as the only choice, no matter
    /// what arrives later: the data was seen. Data which may be used with any provider marks the holder
    /// as one which holds data of a data source, while a restriction set earlier stays. NOT_SPECIFIED
    /// demands nothing and changes nothing. The order does not matter.
    /// </remarks>
    /// <param name="held">The data security held so far.</param>
    /// <param name="arriving">What the data arriving demands.</param>
    /// <returns>The tightened data security; never looser than either of the two.</returns>
    public static DataSourceSecurity TightenedBy(this DataSourceSecurity held, DataSourceSecurity arriving) => (held, arriving) switch
    {
        (DataSourceSecurity.SELF_HOSTED, _) or (_, DataSourceSecurity.SELF_HOSTED) => DataSourceSecurity.SELF_HOSTED,
        (_, DataSourceSecurity.ALLOW_ANY) => DataSourceSecurity.ALLOW_ANY,
        _ => held,
    };

    public static TextColor GetColor(this DataSourceSecurity security) => security switch
    {
        DataSourceSecurity.ALLOW_ANY => TextColor.WARN,
        DataSourceSecurity.SELF_HOSTED => TextColor.SUCCESS,
        
        _ => TextColor.ERROR
    };
}