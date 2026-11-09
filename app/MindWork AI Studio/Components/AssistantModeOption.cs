using AIStudio.Assistants;

namespace AIStudio.Components;

/// <summary>
/// One of the choices an assistant offers before it starts, e.g., to translate a document or a text.
/// </summary>
/// <param name="Mode">What the assistant works on once the user chose this option.</param>
/// <param name="Icon">The icon of the option.</param>
/// <param name="Title">The short name of the option.</param>
/// <param name="Description">What the assistant does with this option.</param>
public readonly record struct AssistantModeOption(AssistantContentMode Mode, string Icon, string Title, string Description);