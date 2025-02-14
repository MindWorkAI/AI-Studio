using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Services;

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
    public async Task CreateFromProviderAsync(IProvider provider, SettingsManager settings, DataSourceService dataSourceService, Model chatModel, IContent? lastPrompt, ChatThread? chatThread, CancellationToken token = default)
    {
        if(chatThread is null)
            return;

        //
        // Check if the user wants to bind any data sources to the chat:
        //
        if (chatThread.UseDataSources)
        {
            //
            // When the user wants to bind data sources to the chat, we
            // have to check if the data sources are available for the
            // selected provider. Also, we have to check if any ERI
            // data sources changed its security requirements.
            //
            List<IDataSource> preselectedDataSources = chatThread.DataSourceOptions.PreselectedDataSourceIds.Select(id => settings.ConfigurationData.DataSources.FirstOrDefault(ds => ds.Id == id)).Where(ds => ds is not null).ToList()!;
            var dataSources = await dataSourceService.GetDataSources(provider, preselectedDataSources);
            var selectedDataSources = dataSources.SelectedDataSources;
            
            //
            // Should the AI select the data sources?
            //
            if (chatThread.DataSourceOptions.AutomaticDataSourceSelection)
            {
                // TODO: Start agent based on allowed data sources.
            }

            //
            // Trigger the retrieval part of the (R)AG process:
            //

            //
            // Perform the augmentation of the R(A)G process:
            //
        }

        // Store the last time we got a response. We use this later
        // to determine whether we should notify the UI about the
        // new content or not. Depends on the energy saving mode
        // the user chose.
        var last = DateTimeOffset.Now;

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
        
        // Inform the UI that the streaming is done:
        await this.StreamingDone();
    }

    #endregion
    
    /// <summary>
    /// The text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}