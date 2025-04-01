using AIStudio.Assistants.ERI;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Settings;

public interface IERIDataSource : IExternalDataSource
{
    /// <summary>
    /// The hostname of the ERI server.
    /// </summary>
    public string Hostname { get; init; }
    
    /// <summary>
    /// The port of the ERI server.
    /// </summary>
    public int Port { get; init; }
    
    /// <summary>
    /// The authentication method to use.
    /// </summary>
    public AuthMethod AuthMethod { get; init; }
    
    /// <summary>
    /// The username to use for authentication, when the auth. method is USERNAME_PASSWORD.
    /// </summary>
    public string Username { get; init; }
    
    /// <summary>
    /// The ERI specification to use.
    /// </summary>
    public ERIVersion Version { get; init; }
    
    /// <summary>
    /// The ID of the selected retrieval process.
    /// </summary>
    public string SelectedRetrievalId { get; init; }
}