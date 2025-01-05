namespace AIStudio.Settings.DataModel;

public static class DataSourceExtension
{
    public static string GetDisplayName(this DataSourceType type)
    {
        return type switch
        {
            DataSourceType.LOCAL_FILE => "Local File",
            DataSourceType.LOCAL_DIRECTORY => "Local Directory",
            DataSourceType.ERI => "ERI Server",
            
            _ => "None",
        };
    }
}