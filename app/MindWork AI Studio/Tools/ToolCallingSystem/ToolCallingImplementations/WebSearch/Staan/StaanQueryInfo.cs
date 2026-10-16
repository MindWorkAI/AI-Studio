namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;

/// <summary>
/// What Staan says about the query it actually ran.
/// </summary>
internal sealed record StaanQueryInfo
{
    /// <summary>
    /// The query Staan searched for after correcting it, or empty when it searched what it was given.
    /// </summary>
    public string AlteredQuery { get; init; } = string.Empty;
}