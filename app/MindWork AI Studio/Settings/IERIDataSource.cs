using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Settings;

public interface IERIDataSource : IExternalDataSource
{
    public string Hostname { get; init; }
    
    public int Port { get; init; }
    
    public AuthMethod AuthMethod { get; init; }
}