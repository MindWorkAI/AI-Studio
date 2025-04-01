using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.Services;

namespace AIStudio.Agents;

public sealed class AgentDataSourceSelection (ILogger<AgentDataSourceSelection> logger, ILogger<AgentBase> baseLogger, SettingsManager settingsManager, DataSourceService dataSourceService, ThreadSafeRandom rng) : AgentBase(baseLogger, settingsManager, dataSourceService, rng)
{
    private readonly List<ContentBlock> answers = new();
    
    #region Overrides of AgentBase

    /// <inheritdoc />
    protected override Type Type => Type.SYSTEM;
    
    /// <inheritdoc />
    public override string Id => "Data Source Selection";

    /// <inheritdoc />
    protected override string JobDescription =>
        """
        You receive a system and a user prompt, as well as a list of possible data sources as input.
        Your task is to select the appropriate data sources for the given task. You may choose none,
        one, or multiple sources, depending on what best fits the system and user prompt. You need
        to estimate and assess which source, based on its description, might be helpful in
        processing the prompts.
        
        Your response is a JSON list in the following format:
        
        ```
        [
          {"id": "The data source ID", "reason": "Why did you choose this source?", "confidence": 0.87},
          {"id": "The data source ID", "reason": "Why did you choose this source?", "confidence": 0.54}
        ]
        ```
        
        You express your confidence as a floating-point number between 0.0 (maximum uncertainty) and
        1.0 (you are absolutely certain that this source is needed).
        
        The JSON schema is:
        
        ```
        {
          "$schema": "http://json-schema.org/draft-04/schema#",
          "type": "array", 
          "items": [
            {
              "type": "object",
              "properties": {
                "id": {
                  "type": "string"
                },
                "reason": {
                  "type": "string"
                },
                "confidence": {
                  "type": "number"
                }
              },
              "required": [
                "id",
                "reason",
                "confidence"
              ]
            }
          ]
        }
        ```
        
        When no data source is needed, you return an empty JSON list `[]`. You do not ask any
        follow-up questions. You do not address the user. Your response consists solely of
        the JSON list.
        """;
    
    /// <inheritdoc />
    protected override string SystemPrompt(string availableDataSources) => $"""
                                                                      {this.JobDescription}
                                                                      
                                                                      {availableDataSources}
                                                                      """;

    /// <inheritdoc />
    public override Settings.Provider? ProviderSettings { get; set; }
    
    /// <summary>
    /// The data source selection agent does not work with context. Use
    /// the process input method instead.
    /// </summary>
    /// <returns>The chat thread without any changes.</returns>
    public override Task<ChatThread> ProcessContext(ChatThread chatThread, IDictionary<string, string> additionalData) => Task.FromResult(chatThread);

    /// <inheritdoc />
    public override async Task<ContentBlock> ProcessInput(ContentBlock input, IDictionary<string, string> additionalData)
    {
        if (input.Content is not ContentText text)
            return EMPTY_BLOCK;
        
        if(text.InitialRemoteWait || text.IsStreaming)
            return EMPTY_BLOCK;
        
        if(string.IsNullOrWhiteSpace(text.Text))
            return EMPTY_BLOCK;
        
        if(!additionalData.TryGetValue("availableDataSources", out var availableDataSources) || string.IsNullOrWhiteSpace(availableDataSources))
            return EMPTY_BLOCK;
        
        var thread = this.CreateChatThread(this.SystemPrompt(availableDataSources));
        var userRequest = this.AddUserRequest(thread, text.Text);
        await this.AddAIResponseAsync(thread, userRequest.UserPrompt, userRequest.Time);
        
        var answer = thread.Blocks[^1];
        
        this.answers.Add(answer);
        return answer;
    }

    // <inheritdoc />
    public override Task<bool> MadeDecision(ContentBlock input) => Task.FromResult(true);

    // <inheritdoc />
    public override IReadOnlyCollection<ContentBlock> GetContext() => [];

    // <inheritdoc />
    public override IReadOnlyCollection<ContentBlock> GetAnswers() => this.answers;

    #endregion

    public async Task<List<SelectedDataSource>> PerformSelectionAsync(IProvider provider, IContent lastPrompt, ChatThread chatThread, AllowedSelectedDataSources dataSources, CancellationToken token = default)
    {
        logger.LogInformation("The AI should select the appropriate data sources.");

        //
        // 1. Which LLM provider should the agent use?
        //

        // We start with the provider currently selected by the user:
        var agentProvider = this.SettingsManager.GetPreselectedProvider(Tools.Components.AGENT_DATA_SOURCE_SELECTION, provider.Id, true);

        // Assign the provider settings to the agent:
        logger.LogInformation($"The agent for the data source selection uses the provider '{agentProvider.InstanceName}' ({agentProvider.UsedLLMProvider.ToName()}, confidence={agentProvider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level.GetName()}).");
        this.ProviderSettings = agentProvider;

        //
        // 2. Prepare the current system and user prompts as input for the agent: 
        //
        var lastPromptContent = lastPrompt switch
        {
            ContentText text => text.Text,

            // Image prompts may be empty, e.g., when the image is too large:
            ContentImage image => await image.AsBase64(token),

            // Other content types are not supported yet:
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(lastPromptContent))
        {
            logger.LogWarning("The last prompt is empty. The AI cannot select data sources.");
            return [];
        }

        //
        // 3. Prepare the allowed data sources as input for the agent:
        //
        var additionalData = new Dictionary<string, string>();
        logger.LogInformation("Preparing the list of allowed data sources for the agent to choose from.");
        
        // Notice: We do not dispose the Rust service here. The Rust service is a singleton
        // and will be disposed when the application shuts down:
        var rustService = Program.SERVICE_PROVIDER.GetService<RustService>()!;
        
        var sb = new StringBuilder();
        sb.AppendLine("The following data sources are available for selection:");
        foreach (var ds in dataSources.AllowedDataSources)
        {
            switch (ds)
            {
                case DataSourceLocalDirectory localDirectory:
                    sb.AppendLine($"- Id={ds.Id}, name='{localDirectory.Name}', type=local directory, path='{localDirectory.Path}'");
                    break;

                case DataSourceLocalFile localFile:
                    sb.AppendLine($"- Id={ds.Id}, name='{localFile.Name}', type=local file, path='{localFile.FilePath}'");
                    break;

                case IERIDataSource eriDataSource:
                    var eriServerDescription = string.Empty;

                    try
                    {
                        //
                        // Call the ERI server to get the server description:
                        //
                        using var eriClient = ERIClientFactory.Get(eriDataSource.Version, eriDataSource)!;
                        var authResponse = await eriClient.AuthenticateAsync(rustService, cancellationToken: token);
                        if (authResponse.Successful)
                        {
                            var serverDescriptionResponse = await eriClient.GetDataSourceInfoAsync(token);
                            if (serverDescriptionResponse.Successful)
                            {
                                eriServerDescription = serverDescriptionResponse.Data.Description;

                                // Remove all line breaks from the description:
                                eriServerDescription = eriServerDescription.Replace("\n", " ").Replace("\r", " ");
                            }
                            else
                                logger.LogWarning($"Was not able to retrieve the server description from the ERI data source '{eriDataSource.Name}'. Message: {serverDescriptionResponse.Message}");
                        }
                        else
                            logger.LogWarning($"Was not able to authenticate with the ERI data source '{eriDataSource.Name}'. Message: {authResponse.Message}");
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"The ERI data source '{eriDataSource.Name}' is not available. Thus, we cannot retrieve the server description. Error: {e.Message}");
                    }

                    //
                    // Append the ERI data source to the list. Use the server description if available:
                    //
                    if (string.IsNullOrWhiteSpace(eriServerDescription))
                        sb.AppendLine($"- Id={ds.Id}, name='{eriDataSource.Name}', type=external data source");
                    else
                        sb.AppendLine($"- Id={ds.Id}, name='{eriDataSource.Name}', type=external data source, description='{eriServerDescription}'");

                    break;
            }
        }

        logger.LogInformation("Prepared the list of allowed data sources for the agent.");
        additionalData.Add("availableDataSources", sb.ToString());

        //
        // 4. Let the agent select the data sources:
        //
        var prompt = $"""
                      The system prompt is:

                      ```
                      {chatThread.SystemPrompt}
                      ```

                      The user prompt is:

                      ```
                      {lastPromptContent}
                      ```
                      """;

        // Call the agent:
        var aiResponse = await this.ProcessInput(new ContentBlock
        {
            Time = DateTimeOffset.UtcNow,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = new ContentText
            {
                Text = prompt,
            },
        }, additionalData);

        if(aiResponse.Content is null)
        {
            logger.LogWarning("The agent did not return a response.");
            return [];
        }

        switch (aiResponse)
        {
            
            //
            // 5. Parse the agent response:
            //
            case { ContentType: ContentType.TEXT, Content: ContentText textContent }:
            {
                //
                // What we expect is a JSON list of SelectedDataSource objects:
                //
                var selectedDataSourcesJson = textContent.Text;
                
                //
                // We know how bad LLM may be in generating JSON without surrounding text.
                // Thus, we expect the worst and try to extract the JSON list from the text:
                //
                var json = ExtractJson(selectedDataSourcesJson);
                
                try
                {
                    var aiSelectedDataSources = JsonSerializer.Deserialize<List<SelectedDataSource>>(json, JSON_SERIALIZER_OPTIONS);
                    return aiSelectedDataSources ?? [];
                }
                catch
                {
                    logger.LogWarning("The agent answered with an invalid or unexpected JSON format.");
                    return [];
                }
            }
            
            case { ContentType: ContentType.TEXT }:
                logger.LogWarning("The agent answered with an unexpected inner content type.");
                return [];
            
            case { ContentType: ContentType.NONE }:
                logger.LogWarning("The agent did not return a response.");
                return [];
            
            default:
                logger.LogWarning($"The agent answered with an unexpected content type '{aiResponse.ContentType}'.");
                return [];
        }
    }

    /// <summary>
    /// Extracts the JSON list from the given text. The text may contain additional
    /// information around the JSON list. The method tries to extract the JSON list
    /// from the text.
    /// </summary>
    /// <remarks>
    /// Algorithm: The method searches for the first line that contains only a '[' character.
    /// Then, it searches for the first line that contains only a ']' character. The method
    /// returns the text between these two lines (including the brackets). When the method
    /// cannot find the JSON list, it returns an empty string.
    /// </remarks>
    /// <param name="text">The text that may contain the JSON list.</param>
    /// <returns>The extracted JSON list.</returns>
    private static ReadOnlySpan<char> ExtractJson(ReadOnlySpan<char> text)
    {
        var startIndex = -1;
        var endIndex = -1;
        var foundStart = false;
        var foundEnd = false;
        var lineStart = 0;
        
        for (var i = 0; i <= text.Length; i++)
        {
            // Handle the end of the line or the end of the text:
            if (i == text.Length || text[i] == '\n')
            {
                if (IsCharacterAloneInLine(text, lineStart, i, '[') && !foundStart)
                {
                    startIndex = lineStart;
                    foundStart = true;
                }
                else if (IsCharacterAloneInLine(text, lineStart, i, ']') && foundStart && !foundEnd)
                {
                    endIndex = i;
                    foundEnd = true;
                    break;
                }
                
                lineStart = i + 1;
            }
        }
        
        if (foundStart && foundEnd)
        {
            // Adjust endIndex for slicing, ensuring it's within bounds:
            return text.Slice(startIndex, Math.Min(text.Length, endIndex + 1) - startIndex);
        }
        
        return ReadOnlySpan<char>.Empty;
    }
    
    private static bool IsCharacterAloneInLine(ReadOnlySpan<char> text, int lineStart, int lineEnd, char character)
    {
        for (var i = lineStart; i < lineEnd; i++)
            if (!char.IsWhiteSpace(text[i]) && text[i] != character)
                return false;

        return true;
    }
}