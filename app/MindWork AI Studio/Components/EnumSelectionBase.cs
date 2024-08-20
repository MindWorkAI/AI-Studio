using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public abstract class EnumSelectionBase : ComponentBase
{
    protected static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
}