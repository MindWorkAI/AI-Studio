using AIStudio.Assistants.ERI;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Settings;

public interface IERIDataSource : IExternalDataSource
{
    /// <summary>
    /// The hostname of the ERI server.
    /// </summary>
    public string Hostname { get; set; }
    
    /// <summary>
    /// The port of the ERI server.
    /// </summary>
    public int Port { get; set; }
    
    /// <summary>
    /// The authentication method to use.
    /// </summary>
    public AuthMethod AuthMethod { get; set; }
    
    /// <summary>
    /// The username to use for authentication, when the auth. method is USERNAME_PASSWORD.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// How username/password authentication should obtain the username.
    /// </summary>
    public DataSourceERIUsernamePasswordMode UsernamePasswordMode { get; set; }
    
    /// <summary>
    /// The ERI specification to use.
    /// </summary>
    public ERIVersion Version { get; set; }
    
    /// <summary>
    /// The ID of the selected retrieval process.
    /// </summary>
    public string SelectedRetrievalId { get; set; }
}