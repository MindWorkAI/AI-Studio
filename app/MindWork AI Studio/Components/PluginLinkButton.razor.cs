using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Opens a link from the metadata of a plugin: a website in the browser, or a new mail in the mail
/// program.
/// </summary>
/// <remarks>
/// A support contact may also be plain text, such as a name or a phone number. For a text which is no
/// link, the button does not appear at all. The Assistant Builder fills the support contact and the
/// source URL with links to MindWork AI, because every plugin has to state both. Those links tell nothing
/// about the plugin itself, so their button stays disabled.
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

    private static bool IsPlaceholder(Uri link) => link.OriginalString is AssistantPluginGenerationService.DEFAULT_SUPPORT_CONTACT or AssistantPluginGenerationService.DEFAULT_SOURCE_URL;
}