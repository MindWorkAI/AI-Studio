using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.AIOperations.DataSourceChoice;

/// <summary>
/// Chooses the data sources which fit a prompt, from the descriptions of the data sources.
/// </summary>
/// <remarks>
/// The classic RAG process runs it before it searches, see AgenticSrcSelWithDynHeur.
/// </remarks>
public sealed class DataSourceChoiceOperation(ILogger<DataSourceChoiceOperation> logger, SettingsManager settingsManager, DataSourceDescriptionService descriptionService) : AIOperationBase<DataSourceChoiceRequest, DataSourceChoiceResult>(logger, settingsManager)
{
    private const string JOB_DESCRIPTION =
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

    /// <summary>
    /// Reads the provider settings of the operation.
    /// </summary>
    /// <param name="settingsManager">The settings to read.</param>
    /// <returns>The preferred provider and the minimum confidence, or none of them while the preselection is switched off.</returns>
    public static AIOperationProviderSettings GetProviderSettings(SettingsManager settingsManager)
    {
        var settings = settingsManager.ConfigurationData.AgentDataSourceSelection;
        return settings.PreselectAgentOptions
            ? new(settings.PreselectedAgentProvider, settings.MinimumProviderConfidence)
            : AIOperationProviderSettings.NONE;
    }

    #region Overrides of AIOperationBase

    /// <inheritdoc />
    public override string Id => "Data Source Selection";

    /// <inheritdoc />
    public override Tools.Components Component => Tools.Components.AGENT_DATA_SOURCE_SELECTION;

    /// <summary>
    /// The model reads the descriptions of the data sources, so it has to reach what they require.
    /// </summary>
    public override ConfidenceLevel GetRequiredConfidence(DataSourceChoiceRequest request) => request.DataSources.GetRequiredConfidenceLevel();

    /// <inheritdoc />
    public override async Task<DataSourceChoiceResult> RunAsync(DataSourceChoiceRequest request, AIOperationContext context, CancellationToken token = default)
    {
        this.Logger.LogInformation("The AI should select the appropriate data sources.");

        //
        // 1. Prepare the last prompt of the user as input:
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
            this.Logger.LogWarning("The last prompt is empty. The AI cannot select data sources.");
            return DataSourceChoiceResult.NOT_ANSWERED;
        }

        //
        // 2. Prepare the data sources to choose from:
        //
        var availableDataSources = new StringBuilder();
        availableDataSources.AppendLineLF("The following data sources are available for selection:");
        foreach (var dataSource in request.DataSources)
        {
            var description = await descriptionService.GetDescriptionAsync(dataSource, token);
            var descriptionPart = string.IsNullOrWhiteSpace(description) ? string.Empty : $", description='{description}'";
            switch (dataSource)
            {
                case DataSourceLocalDirectory localDirectory:
                    availableDataSources.AppendLineLF($"- Id={dataSource.Id}, name='{localDirectory.Name}', type=local directory, path='{localDirectory.Path}'{descriptionPart}");
                    break;

                case DataSourceLocalFile localFile:
                    availableDataSources.AppendLineLF($"- Id={dataSource.Id}, name='{localFile.Name}', type=local file, path='{localFile.FilePath}'{descriptionPart}");
                    break;

                case IERIDataSource eriDataSource:
                    availableDataSources.AppendLineLF($"- Id={dataSource.Id}, name='{eriDataSource.Name}', type=external data source{descriptionPart}");
                    break;
            }
        }

        var systemPrompt = $"""
                            {JOB_DESCRIPTION}

                            {availableDataSources}
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

        //
        // 3. Let the model choose:
        //
        var answer = await this.AskAsync(request, context, systemPrompt, userPrompt, token);
        if (answer is null)
            return DataSourceChoiceResult.NOT_ANSWERED;

        //
        // 4. Read the answer. We know how bad models may be at writing JSON without surrounding text,
        //    so we expect the worst and extract the JSON list from the text:
        //
        var json = ModelAnswerJson.ExtractObjectList(answer);
        try
        {
            var selectedDataSources = JsonSerializer.Deserialize<List<SelectedDataSource>>(json, AIOperationsJson.OPTIONS);
            return new DataSourceChoiceResult(selectedDataSources ?? [], true);
        }
        catch
        {
            this.Logger.LogWarning("The AI answered with an invalid or unexpected JSON format.");
            return DataSourceChoiceResult.NOT_ANSWERED;
        }
    }

    #endregion
}
