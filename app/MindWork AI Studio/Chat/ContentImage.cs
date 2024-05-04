using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.JSInterop;

namespace AIStudio.Chat;

/// <summary>
/// Represents an image inside the chat.
/// </summary>
public sealed class ContentImage : IContent
{
    #region Implementation of IContent

    /// <inheritdoc />
    public bool InitialRemoteWait { get; set; } = false;

    /// <inheritdoc />
    public bool IsStreaming { get; set; } = false;

    /// <inheritdoc />
    public Func<Task> StreamingDone { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    public Func<Task> StreamingEvent { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    public Task CreateFromProviderAsync(IProvider provider, IJSRuntime jsRuntime, SettingsManager settings, Model chatModel, ChatThread chatChatThread, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    #endregion

    /// <summary>
    /// The URL of the image.
    /// </summary>
    public string URL { get; set; } = string.Empty;

    /// <summary>
    /// The local path of the image.
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;
}