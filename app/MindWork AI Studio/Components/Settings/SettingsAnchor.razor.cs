using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

/// <summary>
/// The whole part of a settings page which leads to a deeper page, such as the section of the chat
/// templates on the page of the chat.
/// </summary>
/// <remarks>
/// <para>
/// Coming back up from the deeper page, the settings window shows this part again: it scrolls it into
/// view and gives it the focus, whose frame stays until the user clicks elsewhere, see
/// <see cref="AIStudio.Dialogs.Settings.SettingsWindow"/>.
/// So the user sees where the deeper page lives, also when they opened it from elsewhere, such as from
/// the chat.
/// </para>
/// <para>
/// The part is found by the path of the page it leads to, so nothing has to be registered, and it is
/// found whatever the user did on the deeper page, also when they deleted the entry they had clicked.
/// Every part of a page which leads further belongs into one of these.
/// </para>
/// </remarks>
public partial class SettingsAnchor : ComponentBase
{
    /// <summary>
    /// The path of the deeper page this part leads to.
    /// </summary>
    [Parameter]
    public SettingsPath Path { get; set; }

    /// <summary>
    /// The part of the page itself.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}