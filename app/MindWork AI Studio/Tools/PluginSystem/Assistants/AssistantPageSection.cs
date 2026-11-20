namespace AIStudio.Tools.PluginSystem.Assistants;

/// <summary>
/// One section of the assistants page, with the assistant plugins it shows.
/// </summary>
/// <param name="Category">The category of the section.</param>
/// <param name="Assistants">The assistant plugins of this category, sorted by their title.</param>
/// <typeparam name="TPlugin">The type of the plugins.</typeparam>
public sealed record AssistantPageSection<TPlugin>(AssistantCategoryKey Category, IReadOnlyList<TPlugin> Assistants);