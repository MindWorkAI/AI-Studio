using System.Text;
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
    private static readonly ILogger<ContentText> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ContentText>();
    
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
    [JsonIgnore]
    public bool IsStreaming { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingDone { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingEvent { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    public List<Source> Sources { get; set; } = [];
    
    /// <inheritdoc />
    public List<string> FileAttachments { get; set; } = [];

    /// <inheritdoc />
    public async Task<ChatThread> CreateFromProviderAsync(IProvider provider, Model chatModel, IContent? lastUserPrompt, ChatThread? chatThread, CancellationToken token = default)
    {
        if(chatThread is null)
            return new();
        
        if(!chatThread.IsLLMProviderAllowed(provider))
        {
            LOGGER.LogError("The provider is not allowed for this chat thread due to data security reasons. Skipping the AI process.");
            return chatThread;
        }

        // Call the RAG process. Right now, we only have one RAG process:
        if (lastUserPrompt is not null)
        {
            try
            {
                var rag = new AISrcSelWithRetCtxVal();
                chatThread = await rag.ProcessAsync(provider, lastUserPrompt, chatThread, token);
            }
            catch (Exception e)
            {
                LOGGER.LogError(e, "Skipping the RAG process due to an error.");
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
            await foreach (var contentStreamChunk in provider.StreamChatCompletion(chatModel, chatThread, settings, token))
            {
                // When the user cancels the request, we stop the loop:
                if (token.IsCancellationRequested)
                    break;

                // Stop the waiting animation:
                this.InitialRemoteWait = false;
                this.IsStreaming = true;
                
                // Add the response to the text:
                this.Text += contentStreamChunk;
                
                // Merge the sources:
                this.Sources.MergeSources(contentStreamChunk.Sources);
                
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
        Sources = [..this.Sources],
        FileAttachments = [..this.FileAttachments],
    };

    #endregion

    public async Task<string> PrepareContentForAI()
    {
        var sb = new StringBuilder();
        sb.AppendLine(this.Text);

        if(this.FileAttachments.Count > 0)
        {
            // Filter out files that no longer exist
            var existingFiles = this.FileAttachments.Where(File.Exists).ToList();

            // Log warning for missing files
            var missingFiles = this.FileAttachments.Except(existingFiles).ToList();
            if (missingFiles.Count > 0)
                foreach (var missingFile in missingFiles)
                    LOGGER.LogWarning("File attachment no longer exists and will be skipped: '{MissingFile}'", missingFile);

            // Only proceed if there are existing files
            if (existingFiles.Count > 0)
            {
                // Check Pandoc availability once before processing file attachments
                var pandocState = await Pandoc.CheckAvailabilityAsync(Program.RUST_SERVICE, showMessages: true, showSuccessMessage: false);

                if (!pandocState.IsAvailable)
                    LOGGER.LogWarning("File attachments could not be processed because Pandoc is not available.");
                else if (!pandocState.CheckWasSuccessful)
                    LOGGER.LogWarning("File attachments could not be processed because the Pandoc version check failed.");
                else
                {
                    sb.AppendLine();
                    sb.AppendLine("The following files are attached to this message:");
                    foreach(var file in existingFiles)
                    {
                        sb.AppendLine();
                        sb.AppendLine("---------------------------------------");
                        sb.AppendLine($"File path: {file}");
                        sb.AppendLine("File content:");
                        sb.AppendLine("````");
                        sb.AppendLine(await Program.RUST_SERVICE.ReadArbitraryFileData(file, int.MaxValue));
                        sb.AppendLine("````");
                    }
                }
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// The text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}