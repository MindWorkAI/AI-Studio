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
    /// The type of the image source.
    /// </summary>
    /// <remarks>
    /// Is the image source a URL, a local file path, a base64 string, etc.?
    /// </remarks>
    public required ContentImageSource SourceType { get; init; }

    /// <summary>
    /// The image source.
    /// </summary>
    public required string Source { get; set; }
}