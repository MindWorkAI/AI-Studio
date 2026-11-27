using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

/// <summary>
/// The settings of compressing a chat, and of what happens when a chat fills the context window of its model.
/// </summary>
/// <remarks>
/// Shown in the panel of the settings page and in the dialog which the dialogs of compressing open, see
/// SettingsPanelAITaskChatCompression and SettingsDialogChatCompression, so that both show the same.
/// </remarks>
public partial class ChatCompressionSettings : MSGComponentBase
{
    /// <summary>
    /// The providers to choose from for writing the summary.
    /// </summary>
    [Parameter]
    public IReadOnlyList<ConfigurationSelectData<string>> AvailableLLMProviders { get; set; } = [];
}