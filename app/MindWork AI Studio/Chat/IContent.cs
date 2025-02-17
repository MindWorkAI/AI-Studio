using System.Text.Json.Serialization;

using AIStudio.Provider;

namespace AIStudio.Chat;

/// <summary>
/// The interface for any content in the chat.
/// </summary>
[JsonDerivedType(typeof(ContentText), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ContentImage), typeDiscriminator: "image")]
public interface IContent
{
    /// <summary>
    /// Do we need to wait for the remote, i.e., the AI, to process the related request?
    /// Does not indicate that the stream is finished; it only indicates that we are
    /// waiting for the first response, i.e., wait for the remote to pick up the request.
    /// </summary>
    [JsonIgnore]
    public bool InitialRemoteWait { get; set; }

    /// <summary>
    /// Indicates whether the content is streaming right now. False, if the content is
    /// either static or the stream has finished.
    /// </summary>
    [JsonIgnore]
    public bool IsStreaming { get; set; }

    /// <summary>
    /// An action that is called when the content was changed during streaming.
    /// </summary>
    [JsonIgnore]
    public Func<Task> StreamingEvent { get; set; }

    /// <summary>
    /// An action that is called when the streaming is done.
    /// </summary>
    [JsonIgnore]
    public Func<Task> StreamingDone { get; set; }
    
    /// <summary>
    /// Uses the provider to create the content.
    /// </summary>
    public Task CreateFromProviderAsync(IProvider provider, Model chatModel, IContent? lastPrompt, ChatThread? chatChatThread, CancellationToken token = default);
}