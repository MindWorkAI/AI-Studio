namespace AIStudio.Components;

/// <summary>
/// Represents an assistant block which belongs to an assistant category.
/// </summary>
/// <remarks>
/// Assistant blocks are generic over their settings dialog. This interface gives the category block
/// access to their visibility without the need to know that type parameter.
/// </remarks>
public interface IAssistantCategoryMember
{
    /// <summary>
    /// Gets whether the assistant is visible right now.
    /// </summary>
    bool IsVisible { get; }
}