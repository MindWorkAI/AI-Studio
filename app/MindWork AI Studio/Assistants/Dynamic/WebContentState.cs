namespace AIStudio.Assistants.Dynamic;

public sealed class WebContentState
{
    public string Content { get; set; } = string.Empty;
    public bool Preselect { get; set; }
    public bool PreselectContentCleanerAgent { get; set; }
    public bool AgentIsRunning { get; set; }
}
