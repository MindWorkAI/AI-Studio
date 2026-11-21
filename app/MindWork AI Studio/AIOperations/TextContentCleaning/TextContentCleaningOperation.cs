using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.AIOperations.TextContentCleaning;

/// <summary>
/// Keeps the main content of a web page and removes the rest, such as navigation, advertisements, and cookie banners.
/// </summary>
public sealed class TextContentCleaningOperation(ILogger<TextContentCleaningOperation> logger, SettingsManager settingsManager) : AIOperationBase<TextContentCleaningRequest, TextContentCleaningResult>(logger, settingsManager)
{
    private const string JOB_DESCRIPTION =
        """
        You receive a Markdown document as input. Your goal is to identify the main content of the document
        and return it including Markdown formatting. Remove areas that do not belong to the main part of the
        document. For a blog article, return only the text of the article with its formatting. For a scientific
        paper, only the contents of the paper. Delete elements of navigation, advertisements, HTML artifacts,
        cookie banners, etc. If the content contains images, these images remain. The same applies to links.
        Ensure that links and images are present as valid Markdown:

        - Syntax of links: [link text](URL)
        - Syntax of images: ![alt text](URL)

        If you find relative links or images with relative paths, correct them to absolute paths. For this
        purpose, here is the source URL:
        """;

    /// <summary>
    /// Reads the provider settings of the operation.
    /// </summary>
    /// <param name="settingsManager">The settings to read.</param>
    /// <returns>The preferred provider and the minimum confidence, or none of them while the preselection is switched off.</returns>
    public static AIOperationProviderSettings GetProviderSettings(SettingsManager settingsManager)
    {
        var settings = settingsManager.ConfigurationData.TextContentCleaner;
        return settings.PreselectAgentOptions
            ? new(settings.PreselectedAgentProvider, settings.MinimumProviderConfidence)
            : AIOperationProviderSettings.NONE;
    }

    #region Overrides of AIOperationBase

    /// <inheritdoc />
    public override string Id => "Text Content Cleaning";

    /// <inheritdoc />
    public override Tools.Components Component => Tools.Components.AGENT_TEXT_CONTENT_CLEANER;

    /// <summary>
    /// A web page requires nothing of its own: the user chose to read it with the assistant.
    /// </summary>
    public override ConfidenceLevel GetRequiredConfidence(TextContentCleaningRequest request) => ConfidenceLevel.NONE;

    /// <inheritdoc />
    public override async Task<TextContentCleaningResult> RunAsync(TextContentCleaningRequest request, AIOperationContext context, CancellationToken token = default)
    {
        var uncleaned = new TextContentCleaningResult(request.Markdown, false);
        if (string.IsNullOrWhiteSpace(request.Markdown) || string.IsNullOrWhiteSpace(request.SourceUrl))
            return uncleaned;

        var answer = await this.AskAsync(request, context, $"{JOB_DESCRIPTION} `{request.SourceUrl}`.", request.Markdown, token);
        return answer is null ? uncleaned : new TextContentCleaningResult(answer, true);
    }

    #endregion
}
