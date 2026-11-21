namespace AIStudio.Components;

/// <summary>
/// One of the modes an assistant offers before it starts, e.g., to compare models on one request or on whole tasks.
/// </summary>
/// <typeparam name="TMode">The type of the modes of the assistant.</typeparam>
/// <param name="Mode">The mode the assistant works in once the user chose this option.</param>
/// <param name="Icon">The icon of the option.</param>
/// <param name="Title">The short name of the option.</param>
/// <param name="Description">What the assistant does in this mode.</param>
public readonly record struct AssistantModeOption<TMode>(TMode Mode, string Icon, string Title, string Description);