using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Opens a link from the metadata of a plugin: a website in the browser, or a new mail in the mail
/// program.
/// </summary>
/// <remarks>
/// The button only appears when there is something to open. A support contact may be plain text, such as
/// a name or a phone number, and the links which the Assistant Builder fills in for every plugin tell
/// nothing about the plugin itself. A disabled button would only leave the user wondering why it cannot
/// be clicked.
/// </remarks>
public partial class PluginLinkButton : MSGComponentBase
{
    /// <summary>
    /// The text to link, as the plugin states it.
    /// </summary>
    [Parameter, EditorRequired]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The size of the button.
    /// </summary>
    [Parameter]
    public Size Size { get; set; } = Size.Medium;
}