namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// Possible roles of any chat thread.
/// </summary>
public enum Role
{
    NONE,
    UNKNOWN,
    
    SYSTEM,
    USER,
    AI,
    AGENT,
}