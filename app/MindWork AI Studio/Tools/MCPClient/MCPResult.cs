namespace AIStudio.Tools.MCPClient;

/// <summary>
/// The result of a call against an MCP server.
/// </summary>
/// <typeparam name="T">The type of the data returned on success.</typeparam>
public sealed class MCPResult<T>
{
    /// <summary>
    /// Was the call successful?
    /// </summary>
    public bool Successful { get; init; }

    /// <summary>
    /// When the call was not successful, this will contain a user-facing error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The data returned by the call.
    /// </summary>
    public T? Data { get; init; }
}
