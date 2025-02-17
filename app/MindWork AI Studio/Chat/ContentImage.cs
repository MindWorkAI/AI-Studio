using System.Text.Json.Serialization;

using AIStudio.Provider;

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
    public Task CreateFromProviderAsync(IProvider provider, Model chatModel, IContent? lastPrompt, ChatThread? chatChatThread, CancellationToken token = default)
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

    /// <summary>
    /// Read the image content as a base64 string.
    /// </summary>
    /// <remarks>
    /// The images are directly converted to base64 strings. The maximum
    /// size of the image is around 10 MB. If the image is larger, the method
    /// returns an empty string.
    ///
    /// As of now, this method does no sort of image processing. LLMs usually
    /// do not work with arbitrary image sizes. In the future, we might have
    /// to resize the images before sending them to the model.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The image content as a base64 string; might be empty.</returns>
    public async Task<string> AsBase64(CancellationToken token = default)
    {
        switch (this.SourceType)
        {
            case ContentImageSource.BASE64:
                return this.Source;
            
            case ContentImageSource.URL:
            {
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(this.Source, HttpCompletionOption.ResponseHeadersRead, token);
                if(response.IsSuccessStatusCode)
                {
                    // Read the length of the content:
                    var lengthBytes = response.Content.Headers.ContentLength;
                    if(lengthBytes > 10_000_000)
                        return string.Empty;
                    
                    var bytes = await response.Content.ReadAsByteArrayAsync(token);
                    return Convert.ToBase64String(bytes);
                }

                return string.Empty;
            }

            case ContentImageSource.LOCAL_PATH:
                if(File.Exists(this.Source))
                {
                    // Read the content length:
                    var length = new FileInfo(this.Source).Length;
                    if(length > 10_000_000)
                        return string.Empty;
                    
                    var bytes = await File.ReadAllBytesAsync(this.Source, token);
                    return Convert.ToBase64String(bytes);
                }

                return string.Empty;
            
            default:
                return string.Empty;
        }
    }
}