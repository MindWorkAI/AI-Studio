using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Chat;

/// <summary>
/// Represents an image inside the chat.
/// </summary>
public sealed class ContentImage : IContent
{
    #region Implementation of IContent

    /// <inheritdoc />
    [JsonIgnore]
    public bool InitialRemoteWait { get; set; } = false;

    /// <inheritdoc />
    [JsonIgnore]
    public bool IsStreaming { get; set; } = false;

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingDone { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingEvent { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    public Task CreateFromProviderAsync(IProvider provider, SettingsManager settings, Model chatModel, ChatThread chatChatThread, CancellationToken token = default)
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