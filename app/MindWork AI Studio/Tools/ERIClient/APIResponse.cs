namespace AIStudio.Tools.ERIClient;

public sealed class APIResponse<T>
{
    /// <summary>
    /// Was the API call successful?
    /// </summary>
    public bool Successful { get; set; }
    
    /// <summary>
    /// When the API call was not successful, this will contain the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// The data returned by the API call.
    /// </summary>
    public T? Data { get; set; }
}