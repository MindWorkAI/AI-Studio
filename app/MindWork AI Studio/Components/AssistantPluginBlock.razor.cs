using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The tile of an assistant plugin on the assistants page.
/// </summary>
/// <remarks>
/// A plugin appears in each of its categories, so the page may show this tile more than once. A form
/// assistant opens its page, a direct chat launcher opens its chat right away. The plugin texts are shown
/// as the plugin states them: AI Studio does not translate them.
/// </remarks>
public partial class AssistantPluginBlock : MSGComponentBase
{
    /// <summary>
    /// The assistant plugin this tile starts.
    /// </summary>
    [Parameter, EditorRequired]
    public PluginAssistants Plugin { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    [Inject]
    private DirectChatService DirectChatService { get; init; } = null!;

    private PluginAssistantSecurityState SecurityState => PluginAssistantSecurityResolver.Resolve(this.SettingsManager, this.Plugin);

    private string LaunchLink => this.Plugin.StartsChatDirectly ? string.Empty : $"{Routes.ASSISTANT_DYNAMIC}?assistantId={this.Plugin.Id}";

    /// <summary>
    /// The plugin as the plugin factory lists it, which the delete action needs.
    /// </summary>
    private IAvailablePlugin? AvailablePlugin => PluginFactory.AvailablePlugins.OfType<IAvailablePlugin>().FirstOrDefault(plugin => plugin.Id == this.Plugin.Id);

    private async Task StartAsync()
    {
        if (!this.SecurityState.CanStartAssistant)
            return;

        if (!this.Plugin.StartsChatDirectly)
        {
            this.NavigationManager.NavigateTo(this.LaunchLink);
            return;
        }

        var result = await this.DirectChatService.TryCreateAssistantChatAsync(this.Plugin);
        if (result.Request is null)
        {
            await this.MessageBus.SendError(new(AppIcons.ERROR, result.ErrorMessage));
            return;
        }

        MessageBus.INSTANCE.DeferMessage(this, Event.SEND_TO_CHAT, result.Request);
        this.NavigationManager.NavigateTo(Routes.CHAT);
    }
}