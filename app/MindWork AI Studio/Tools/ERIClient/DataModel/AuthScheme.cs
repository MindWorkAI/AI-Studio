namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// Describes one authentication scheme for this data source. 
/// </summary>
/// <param name="AuthMethod">The method used for authentication, e.g., "API Key," "Username/Password," etc.</param>
/// <param name="AuthFieldMappings">A list of field mappings for the authentication method. The client must know,
/// e.g., how the password field is named in the request.</param> 
public readonly record struct AuthScheme(AuthMethod AuthMethod, List<AuthFieldMapping> AuthFieldMappings);