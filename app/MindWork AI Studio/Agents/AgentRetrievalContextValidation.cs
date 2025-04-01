using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

namespace AIStudio.Agents;

public sealed class AgentRetrievalContextValidation (ILogger<AgentRetrievalContextValidation> logger, ILogger<AgentBase> baseLogger, SettingsManager settingsManager, DataSourceService dataSourceService, ThreadSafeRandom rng) : AgentBase(baseLogger, settingsManager, dataSourceService, rng)
{
    #region Overrides of AgentBase

    /// <inheritdoc />
    protected override Type Type => Type.WORKER;
    
    /// <inheritdoc />
    public override string Id => "Retrieval Context Validation";

    /// <inheritdoc />
    protected override string JobDescription =>
        """
        You receive a system and user prompt as well as a retrieval context as input. Your task is to decide whether this
        retrieval context is helpful in processing the prompts or not. You respond with the decision (true or false),
        your reasoning, and your confidence in this decision.

        Your response is only one JSON object in the following format:

        ```
        {"decision": true, "reason": "Why did you choose this source?", "confidence": 0.87}
        ```

        You express your confidence as a floating-point number between 0.0 (maximum uncertainty) and
        1.0 (you are absolutely certain that this retrieval context is needed).

        The JSON schema is:

        ```
        {
          "$schema": "http://json-schema.org/draft-04/schema#",
          "type": "object",
          "properties": {
            "decision": {
              "type": "boolean"
            },
            "reason": {
              "type": "string"
            },
            "confidence": {
              "type": "number"
            }
          },
          "required": [
            "decision",
            "reason",
            "confidence"
          ]
        }
        ```

        You do not ask any follow-up questions. You do not address the user. Your response consists solely of
        that one JSON object.
        """;
    
    /// <inheritdoc />
    protected override string SystemPrompt(string retrievalContext) => $"""
                                                                        {this.JobDescription}

                                                                        {retrievalContext}
                                                                        """;

    /// <inheritdoc />
    public override Settings.Provider? ProviderSettings { get; set; }
    
    /// <summary>
    /// The retrieval context validation agent does not work with context. Use
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
          
        if(!additionalData.TryGetValue("retrievalContext", out var retrievalContext) || string.IsNullOrWhiteSpace(retrievalContext))
            return EMPTY_BLOCK;
          
        var thread = this.CreateChatThread(this.SystemPrompt(retrievalContext));
        var userRequest = this.AddUserRequest(thread, text.Text);
        await this.AddAIResponseAsync(thread, userRequest.UserPrompt, userRequest.Time);
          
        return thread.Blocks[^1];
    }

    /// <inheritdoc />
    public override Task<bool> MadeDecision(ContentBlock input) => Task.FromResult(true);

    /// <summary>
    /// We do not provide any context. This agent will process many retrieval contexts.
    /// This would block a huge amount of memory.
    /// </summary>
    /// <returns>An empty list.</returns>
    public override IReadOnlyCollection<ContentBlock> GetContext() => [];

    /// <summary>
    /// We do not provide any answers. This agent will process many retrieval contexts.
    /// This would block a huge amount of memory.
    /// </summary>
    /// <returns>An empty list.</returns>
    public override IReadOnlyCollection<ContentBlock> GetAnswers() => [];

    #endregion

    /// <summary>
    /// Sets the LLM provider for the agent.
    /// </summary>
    /// <remarks>
    /// When you have to call the validation in parallel for many retrieval contexts,
    /// you can set the provider once and then call the validation method in parallel.
    /// </remarks>
    /// <param name="provider">The current LLM provider. When the user doesn't preselect an agent provider, the agent uses this provider.</param>
    public void SetLLMProvider(IProvider provider)
    {
        // We start with the provider currently selected by the user:
        var agentProvider = this.SettingsManager.GetPreselectedProvider(Tools.Components.AGENT_RETRIEVAL_CONTEXT_VALIDATION, provider.Id, true);
        
        // Assign the provider settings to the agent:
        logger.LogInformation($"The agent for the retrieval context validation uses the provider '{agentProvider.InstanceName}' ({agentProvider.UsedLLMProvider.ToName()}, confidence={agentProvider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level.GetName()}).");
        this.ProviderSettings = agentProvider;
    }
    
    /// <summary>
    /// Validate all retrieval contexts against the last user and the system prompt.
    /// </summary>
    /// <param name="lastPrompt">The last user prompt.</param>
    /// <param name="chatThread">The chat thread.</param>
    /// <param name="retrievalContexts">All retrieval contexts to validate.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The validation results.</returns>
    public async Task<IReadOnlyList<RetrievalContextValidationResult>> ValidateRetrievalContextsAsync(IContent lastPrompt, ChatThread chatThread, IReadOnlyList<IRetrievalContext> retrievalContexts, CancellationToken token = default)
    {
        // Check if the retrieval context validation is enabled:
        if (!this.SettingsManager.ConfigurationData.AgentRetrievalContextValidation.EnableRetrievalContextValidation)
            return [];
        
        logger.LogInformation($"Validating {retrievalContexts.Count:###,###,###,###} retrieval contexts.");
        
        // Prepare the list of validation tasks:
        var validationTasks = new List<Task<RetrievalContextValidationResult>>(retrievalContexts.Count);
        
        // Read the number of parallel validations:
        var numParallelValidations = 3;
        if(this.SettingsManager.ConfigurationData.AgentRetrievalContextValidation.PreselectAgentOptions)
            numParallelValidations = this.SettingsManager.ConfigurationData.AgentRetrievalContextValidation.NumParallelValidations;
        
        numParallelValidations = Math.Max(1, numParallelValidations);
        
        // Use a semaphore to limit the number of parallel validations:
        using var semaphore = new SemaphoreSlim(numParallelValidations);
        foreach (var retrievalContext in retrievalContexts)
        {
            // Wait for an available slot in the semaphore:
            await semaphore.WaitAsync(token);
            
            // Start the next validation task:
            validationTasks.Add(this.ValidateRetrievalContextAsync(lastPrompt, chatThread, retrievalContext, token, semaphore));
        }

        // Wait for all validation tasks to complete:
        return await Task.WhenAll(validationTasks);
    }

    /// <summary>
    /// Validates the retrieval context against the last user and the system prompt.
    /// </summary>
    /// <remarks>
    /// Probably, you have a lot of retrieval contexts to validate. In this case, you
    /// can call this method in parallel for each retrieval context. You might use
    /// the ValidateRetrievalContextsAsync method to validate all retrieval contexts.
    /// </remarks>
    /// <param name="lastPrompt">The last user prompt.</param>
    /// <param name="chatThread">The chat thread.</param>
    /// <param name="retrievalContext">The retrieval context to validate.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="semaphore">The optional semaphore to limit the number of parallel validations.</param>
    /// <returns>The validation result.</returns>
    public async Task<RetrievalContextValidationResult> ValidateRetrievalContextAsync(IContent lastPrompt, ChatThread chatThread, IRetrievalContext retrievalContext, CancellationToken token = default, SemaphoreSlim? semaphore = null)
    {
        try
        {
            //
            // Check if the validation was canceled. This could happen when the user
            // canceled the validation process or when the validation process took
            // too long:
            //
            if(token.IsCancellationRequested)
                return new(false, "The validation was canceled.", 1.0f, retrievalContext);
            
            //
            // 1. Prepare the current system and user prompts as input for the agent: 
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
                logger.LogWarning("The last prompt is empty. The AI cannot validate the retrieval context.");
                return new(false, "The last prompt was empty.", 1.0f, retrievalContext);
            }

            //
            // 2. Prepare the retrieval context for the agent:
            //
            var additionalData = new Dictionary<string, string>();
            var markdownRetrievalContext = await retrievalContext.AsMarkdown(token: token);
            additionalData.Add("retrievalContext", markdownRetrievalContext);

            //
            // 3. Let the agent validate the retrieval context:
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

            if (aiResponse.Content is null)
            {
                logger.LogWarning("The agent did not return a response.");
                return new(false, "The agent did not return a response.", 1.0f, retrievalContext);
            }

            switch (aiResponse)
            {

                //
                // 4. Parse the agent response:
                //
                case { ContentType: ContentType.TEXT, Content: ContentText textContent }:
                {
                    //
                    // What we expect is one JSON object:
                    //
                    var validationJson = textContent.Text;

                    //
                    // We know how bad LLM may be in generating JSON without surrounding text.
                    // Thus, we expect the worst and try to extract the JSON list from the text:
                    //
                    var json = ExtractJson(validationJson);

                    try
                    {
                        var result = JsonSerializer.Deserialize<RetrievalContextValidationResult>(json, JSON_SERIALIZER_OPTIONS);
                        return result with { RetrievalContext = retrievalContext };
                    }
                    catch
                    {
                        logger.LogWarning("The agent answered with an invalid or unexpected JSON format.");
                        return new(false, "The agent answered with an invalid or unexpected JSON format.", 1.0f, retrievalContext);
                    }
                }

                case { ContentType: ContentType.TEXT }:
                    logger.LogWarning("The agent answered with an unexpected inner content type.");
                    return new(false, "The agent answered with an unexpected inner content type.", 1.0f, retrievalContext);

                case { ContentType: ContentType.NONE }:
                    logger.LogWarning("The agent did not return a response.");
                    return new(false, "The agent did not return a response.", 1.0f, retrievalContext);

                default:
                    logger.LogWarning($"The agent answered with an unexpected content type '{aiResponse.ContentType}'.");
                    return new(false, $"The agent answered with an unexpected content type '{aiResponse.ContentType}'.", 1.0f, retrievalContext);
            }
        }
        finally
        {
            // Release the semaphore slot:
            semaphore?.Release();
        }
    }
    
    private static ReadOnlySpan<char> ExtractJson(ReadOnlySpan<char> input)
    {
        //
        // 1. Expect the best case ;-)
        //
        if (CheckJsonObjectStart(input))
            return ExtractJsonPart(input);

        //
        // 2. Okay, we have some garbage before the
        // JSON object. We expected that...
        //
        for (var index = 0; index < input.Length; index++)
        {
            if (input[index] is '{' && CheckJsonObjectStart(input[index..]))
                return ExtractJsonPart(input[index..]);
        }

        return [];
    }
    
    private static bool CheckJsonObjectStart(ReadOnlySpan<char> area)
    {
        char[] expectedSymbols = ['{', '"', 'd'];
        var symbolIndex = 0;

        foreach (var c in area)
        {
            if (symbolIndex >= expectedSymbols.Length)
                return true;

            if (char.IsWhiteSpace(c))
                continue;

            if (c == expectedSymbols[symbolIndex++])
                continue;

            return false;
        }

        return true;
    }

    private static ReadOnlySpan<char> ExtractJsonPart(ReadOnlySpan<char> input)
    {
        var insideString = false;
        for (var index = 0; index < input.Length; index++)
        {
            if (input[index] is '"')
            {
                insideString = !insideString;
                continue;
            }

            if (insideString)
                continue;

            if (input[index] is '}')
                return input[..++index];
        }

        return [];
    }
}