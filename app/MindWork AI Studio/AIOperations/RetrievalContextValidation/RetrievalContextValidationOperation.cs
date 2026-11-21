using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.RAG;

namespace AIStudio.AIOperations.RetrievalContextValidation;

/// <summary>
/// Judges for each passage found by the classic RAG process whether it helps with the prompt.
/// </summary>
/// <remarks>
/// It asks one request per passage, several at once, as many as NumParallelValidations allows. The limit
/// holds per call: two chats validating at the same time each get their own.
/// </remarks>
public sealed class RetrievalContextValidationOperation(ILogger<RetrievalContextValidationOperation> logger, SettingsManager settingsManager) : AIOperationBase<RetrievalContextValidationRequest, IReadOnlyList<RetrievalContextValidationResult>>(logger, settingsManager)
{
    private const int DEFAULT_PARALLEL_VALIDATIONS = 3;

    private const string JOB_DESCRIPTION =
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

    /// <summary>
    /// Reads the provider settings of the operation.
    /// </summary>
    /// <param name="settingsManager">The settings to read.</param>
    /// <returns>The preferred provider and the minimum confidence, or none of them while the preselection is switched off.</returns>
    public static AIOperationProviderSettings GetProviderSettings(SettingsManager settingsManager)
    {
        var settings = settingsManager.ConfigurationData.AgentRetrievalContextValidation;
        return settings.PreselectAgentOptions
            ? new(settings.PreselectedAgentProvider, settings.MinimumProviderConfidence)
            : AIOperationProviderSettings.NONE;
    }

    #region Overrides of AIOperationBase

    /// <inheritdoc />
    public override string Id => "Retrieval Context Validation";

    /// <inheritdoc />
    public override Tools.Components Component => Tools.Components.AGENT_RETRIEVAL_CONTEXT_VALIDATION;

    /// <summary>
    /// The model reads the passages, so it has to reach what the chat requires of every provider which reads its data.
    /// </summary>
    public override ConfidenceLevel GetRequiredConfidence(RetrievalContextValidationRequest request) => request.RequiredConfidence;

    /// <inheritdoc />
    public override async Task<IReadOnlyList<RetrievalContextValidationResult>> RunAsync(RetrievalContextValidationRequest request, AIOperationContext context, CancellationToken token = default)
    {
        this.Logger.LogInformation($"Validating {request.RetrievalContexts.Count:###,###,###,###} retrieval contexts.");

        //
        // The last prompt of the user is the same for every passage, so it is prepared once:
        //
        var lastPromptContent = request.LastUserPrompt switch
        {
            ContentText text => text.Text,

            // Image prompts may be empty, e.g., when the image is too large:
            ContentImage image => await image.TryAsBase64(token) is (success: true, { } base64Image)
                ? base64Image
                : string.Empty,

            // Other content types are not supported yet:
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(lastPromptContent))
        {
            this.Logger.LogWarning("The last prompt is empty. The AI cannot validate the retrieval contexts.");
            return request.RetrievalContexts.Select(retrievalContext => RetrievalContextValidationResult.NotAnswered(retrievalContext, "The last prompt was empty.")).ToList();
        }

        var settings = this.SettingsManager.ConfigurationData.AgentRetrievalContextValidation;
        var numParallelValidations = settings.PreselectAgentOptions ? settings.NumParallelValidations : DEFAULT_PARALLEL_VALIDATIONS;

        //
        // A semaphore of this call limits the validations running at once. It is local, so that
        // another chat validating at the same time does not share the limit of this one:
        //
        using var semaphore = new SemaphoreSlim(Math.Max(1, numParallelValidations));
        var validationTasks = new List<Task<RetrievalContextValidationResult>>(request.RetrievalContexts.Count);
        try
        {
            foreach (var retrievalContext in request.RetrievalContexts)
            {
                await semaphore.WaitAsync(token);
                validationTasks.Add(this.ValidateAsync(request, context, lastPromptContent, retrievalContext, semaphore, token));
            }
        }
        catch (OperationCanceledException)
        {
            //
            // Each validation releases its slot when it ends, and the semaphore is disposed as soon
            // as this method returns. The validations already running got the same token and stop
            // soon; this call waits for them, so that none of them releases a disposed semaphore:
            //
            try
            {
                await Task.WhenAll(validationTasks);
            }
            catch (OperationCanceledException)
            {
                // Expected: they stop because of the same token.
            }
            catch (Exception exception)
            {
                this.Logger.LogWarning(exception, "A retrieval context validation failed while the validations were being canceled.");
            }

            throw;
        }

        return await Task.WhenAll(validationTasks);
    }

    #endregion

    /// <summary>
    /// Validates one passage and releases its slot of the semaphore afterward.
    /// </summary>
    private async Task<RetrievalContextValidationResult> ValidateAsync(RetrievalContextValidationRequest request, AIOperationContext context, string lastPromptContent, PreparedRetrievalContext retrievalContext, SemaphoreSlim semaphore, CancellationToken token)
    {
        try
        {
            //
            // The validation may be canceled while it waits, e.g., by the user or because it took
            // too long:
            //
            if (token.IsCancellationRequested)
                return RetrievalContextValidationResult.NotAnswered(retrievalContext, "The validation was canceled.");

            //
            // The model reads the passage exactly as the chat model would, filtered once for both,
            // so the check judges what is actually sent:
            //
            var systemPrompt = $"""
                                {JOB_DESCRIPTION}

                                {retrievalContext.ToMarkdown()}
                                """;

            var userPrompt = $"""
                              The system prompt is:

                              ```
                              {request.SystemPrompt}
                              ```

                              The user prompt is:

                              ```
                              {lastPromptContent}
                              ```
                              """;

            var answer = await this.AskAsync(request, context, systemPrompt, userPrompt, token);
            if (answer is null)
            {
                this.Logger.LogWarning("The AI did not return a response to the validation of a retrieval context.");
                return RetrievalContextValidationResult.NotAnswered(retrievalContext, "The AI did not return a response.");
            }

            var result = ReadAnswer(answer, retrievalContext);
            if (!result.IsAnswered)
                this.Logger.LogWarning($"The AI gave no usable answer to the validation of a retrieval context: {result.Reason}");

            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Reads what a model answered about one retrieval context.
    /// </summary>
    /// <remarks>
    /// We know how bad models may be at writing JSON without surrounding text, so we expect the worst and
    /// extract the JSON object from the text. The answer counts only when it holds a decision and a confidence
    /// between 0 and 1. Anything else is no usable answer, rather than being read as something the model did
    /// not say: a missing decision would otherwise become false and drop a passage nobody judged.
    /// </remarks>
    /// <param name="answer">The whole answer of the model.</param>
    /// <param name="retrievalContext">The retrieval context the model judged.</param>
    /// <returns>The result, with IsAnswered being false when the answer is not usable.</returns>
    internal static RetrievalContextValidationResult ReadAnswer(string answer, PreparedRetrievalContext retrievalContext)
    {
        RetrievalContextValidationAnswer judgment;
        try
        {
            judgment = JsonSerializer.Deserialize<RetrievalContextValidationAnswer>(ModelAnswerJson.ExtractObject(answer), AIOperationsJson.OPTIONS);
        }
        catch (JsonException)
        {
            return RetrievalContextValidationResult.NotAnswered(retrievalContext, "The AI answered with an invalid or unexpected JSON format.");
        }

        if (judgment.Decision is not { } decision)
            return RetrievalContextValidationResult.NotAnswered(retrievalContext, "The AI answered without a decision.");

        if (judgment.Confidence is not { } confidence || confidence is not (>= 0f and <= 1f))
            return RetrievalContextValidationResult.NotAnswered(retrievalContext, "The AI answered without a confidence between 0 and 1.");

        return new(retrievalContext, true, decision, judgment.Reason ?? string.Empty, confidence);
    }
}