namespace AIStudio.Settings.DataModel;

public static class DataSourceSecurityExtensions
{
    public static string ToSelectionText(this DataSourceSecurity security) => security switch
    {
        DataSourceSecurity.NOT_SPECIFIED => "Please select a security policy",
        
        DataSourceSecurity.ALLOW_ANY => "This data source can be used with any LLM provider. Your data may be sent to a cloud-based provider.",
        DataSourceSecurity.SELF_HOSTED => "This data source can only be used with a self-hosted LLM provider. Your data will not be sent to any cloud-based provider.",
        
        _ => "Unknown security policy"
    };
    
    public static string ToInfoText(this DataSourceSecurity security) => security switch
    {
        DataSourceSecurity.NOT_SPECIFIED => "The security of the data source is not specified yet. You cannot use this data source until you specify a security policy.",
        
        DataSourceSecurity.ALLOW_ANY => "This data source can be used with any LLM provider. Your data may be sent to a cloud-based provider.",
        DataSourceSecurity.SELF_HOSTED => "This data source can only be used with a self-hosted LLM provider. Your data will not be sent to any cloud-based provider.",
        
        _ => "Unknown security policy"
    };
    
    public static TextColor GetColor(this DataSourceSecurity security) => security switch
    {
        DataSourceSecurity.ALLOW_ANY => TextColor.WARN,
        DataSourceSecurity.SELF_HOSTED => TextColor.SUCCESS,
        
        _ => TextColor.ERROR
    };
}