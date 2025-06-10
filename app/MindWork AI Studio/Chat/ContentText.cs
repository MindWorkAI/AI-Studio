using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.RAG.RAGProcesses;

namespace AIStudio.Chat;

/// <summary>
/// Text content in the chat.
/// </summary>
public sealed class ContentText : IContent
{
    /// <summary>
    /// The minimum time between two streaming events, when the user
    /// enables the energy saving mode.
    /// </summary>
    private static readonly TimeSpan MIN_TIME = TimeSpan.FromSeconds(3);

    #region Implementation of IContent

    /// <inheritdoc />
    [JsonIgnore]
    public bool InitialRemoteWait { get; set; }

    /// <inheritdoc />
    // [JsonIgnore]
    public bool IsStreaming { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingDone { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingEvent { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<ChatThread> CreateFromProviderAsync(IProvider provider, Model chatModel, IContent? lastPrompt, ChatThread? chatThread, CancellationToken token = default)
    {
        if(chatThread is null)
            return new();
        
        if(!chatThread.IsLLMProviderAllowed(provider))
        {
            var logger = Program.SERVICE_PROVIDER.GetService<ILogger<ContentText>>()!;
            logger.LogError("The provider is not allowed for this chat thread due to data security reasons. Skipping the AI process.");
            return chatThread;
        }

        // Call the RAG process. Right now, we only have one RAG process:
        if (lastPrompt is not null)
        {
            try
            {
                var rag = new AISrcSelWithRetCtxVal();
                chatThread = await rag.ProcessAsync(provider, lastPrompt, chatThread, token);
            }
            catch (Exception e)
            {
                var logger = Program.SERVICE_PROVIDER.GetService<ILogger<ContentText>>()!;
                logger.LogError(e, "Skipping the RAG process due to an error.");
            }
        }

        // Store the last time we got a response. We use this later
        // to determine whether we should notify the UI about the
        // new content or not. Depends on the energy saving mode
        // the user chose.
        var last = DateTimeOffset.Now;

        // Get the settings manager:
        var settings = Program.SERVICE_PROVIDER.GetService<SettingsManager>()!;
        
        // Start another thread by using a task to uncouple
        // the UI thread from the AI processing:
        await Task.Run(async () =>
        {
            // We show the waiting animation until we get the first response:
            this.InitialRemoteWait = true;
            
            // Iterate over the responses from the AI:
            await foreach (var deltaText in provider.StreamChatCompletion(chatModel, chatThread, settings, token))
            {
                // When the user cancels the request, we stop the loop:
                if (token.IsCancellationRequested)
                    break;

                // Stop the waiting animation:
                this.InitialRemoteWait = false;
                this.IsStreaming = true;
                
                // Add the response to the text:
                this.Text += deltaText;
                
                // Notify the UI that the content has changed,
                // depending on the energy saving mode:
                var now = DateTimeOffset.Now;
                switch (settings.ConfigurationData.App.IsSavingEnergy)
                {
                    // Energy saving mode is off. We notify the UI
                    // as fast as possible -- no matter the odds:
                    case false:
                        await this.StreamingEvent();
                        break;
                    
                    // Energy saving mode is on. We notify the UI
                    // only when the time between two events is
                    // greater than the minimum time:
                    case true when now - last > MIN_TIME:
                        last = now;
                        await this.StreamingEvent();
                        break;
                }
            }

            // Stop the waiting animation (in case the loop
            // was stopped, or no content was received):
            this.InitialRemoteWait = false;
            this.IsStreaming = false;
        }, token);

        this.Text = this.Text.RemoveThinkTags().Trim();
        
        // Inform the UI that the streaming is done:
        await this.StreamingDone();
        return chatThread;
    }

    /// <inheritdoc />
    public IContent DeepClone() => new ContentText
    {
        Text = this.Text,
        InitialRemoteWait = this.InitialRemoteWait,
        IsStreaming = this.IsStreaming,
    };

    #endregion
    
    /// <summary>
    /// The text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}