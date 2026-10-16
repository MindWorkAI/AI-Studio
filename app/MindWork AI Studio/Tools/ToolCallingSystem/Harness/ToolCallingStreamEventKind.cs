namespace AIStudio.Tools.ToolCallingSystem.Harness;

/// <summary>
/// What one event of a streamed tool calling round carries.
/// </summary>
public enum ToolCallingStreamEventKind
{
    NONE = 0,
    
    /// <summary>
    /// A piece of text the model wrote, to be shown while the round is still running.
    /// </summary>
    TEXT_DELTA,
    
    /// <summary>
    /// The round is over and the event carries its outcome.
    /// </summary>
    ROUND_COMPLETED,
}