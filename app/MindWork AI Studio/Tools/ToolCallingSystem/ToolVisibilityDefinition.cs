namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolVisibilityDefinition
{
    public bool Chat { get; init; } = true;

    public bool Assistants { get; init; } = true;

    public List<Components> AllowedComponents { get; init; } = [];

    public List<Components> DeniedComponents { get; init; } = [];

    public bool IsVisibleIn(Components component)
    {
        if (this.AllowedComponents.Count == 0 && this.DeniedComponents.Count == 0)
            return component is Components.CHAT ? this.Chat : this.Assistants;

        var isAllowed = this.AllowedComponents.Count == 0 || this.AllowedComponents.Contains(component);
        return isAllowed && !this.DeniedComponents.Contains(component);
    }
}