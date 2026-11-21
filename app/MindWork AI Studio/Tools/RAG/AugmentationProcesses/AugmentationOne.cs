using System.Text;

using AIStudio.AIOperations;
using AIStudio.AIOperations.RetrievalContextValidation;
using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.RAG.AugmentationProcesses;

public sealed class AugmentationOne : IAugmentationProcess
{
    private static readonly ILogger<AugmentationOne> LOGGER = Program.LOGGER_FACTORY.CreateLogger<AugmentationOne>();

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AugmentationOne).Namespace, nameof(AugmentationOne));

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(AugmentationOne).Namespace, nameof(AugmentationOne));

    #region Implementation of IAugmentationProcess

    /// <inheritdoc />
    public string TechnicalName => "AugmentationOne";

    /// <inheritdoc />
    public string UIName => TB("Standard augmentation process");

    /// <inheritdoc />
    public string Description => TB("This is the standard augmentation process, which augments the chat thread with as many retrieval contexts as the selected model can read.");

    /// <inheritdoc />
    public async Task<AugmentationResult> ProcessAsync(IProvider provider, Model chatModel, IContent lastUserPrompt, ChatThread chatThread, IReadOnlyList<IReadOnlyList<IRetrievalContext>> retrievalContextsByDataSource, CancellationToken token = default)
    {
        var settings = Program.SERVICE_PROVIDER.GetService<SettingsManager>()!;
        var retrievalContexts = retrievalContextsByDataSource.SelectMany(contexts => contexts).ToList();

        if(retrievalContexts.Count == 0)
        {
            LOGGER.LogWarning("No retrieval contexts were issued. Skipping the augmentation process.");
            return new(chatThread, [], [..retrievalContextsByDataSource.Select(_ => 0)]);
        }

        //
        // One report for the whole answer. Every passage is filtered once, before the check of
        // which passages fit: the check then judges exactly what the model would receive, and the
        // model receives exactly what the check judged. The same holds for the budget below.
        //
        var guardService = Program.SERVICE_PROVIDER.GetRequiredService<PromptInjectionGuardService>();
        await using var reportingScope = guardService.BeginAction();
        var preparedContexts = await retrievalContexts.PrepareForModelAsync(guardService, reportingScope, token);
        var usedContexts = preparedContexts;

        // Want the user to validate all retrieval contexts?
        if (settings.ConfigurationData.AgentRetrievalContextValidation.EnableRetrievalContextValidation && chatThread.DataSourceOptions.AutomaticValidation)
        {
            //
            // Which provider validates? Only one which may see what the chat may see: the model reads
            // the passages. One choice serves all passages of this answer.
            //
            var validation = Program.SERVICE_PROVIDER.GetRequiredService<RetrievalContextValidationOperation>();
            var providerResolver = Program.SERVICE_PROVIDER.GetRequiredService<AIOperationProviderResolver>();
            var validationRequest = new RetrievalContextValidationRequest(chatThread.SystemPrompt, lastUserPrompt, preparedContexts, chatThread.RequiredProviderConfidence);
            var validationProviderOptions = ClassicRagProviderOptions.ForDataAccess(settings, provider.ConfiguredProviderId, chatThread.DataSecurity, chatThread.RequiredProviderConfidence);
            var validationChoice = providerResolver.Resolve(validation, validationRequest, RetrievalContextValidationOperation.GetProviderSettings(settings), validationProviderOptions);
            if (validationChoice.HasProvider)
            {
                try
                {
                    // Let's validate all retrieval contexts:
                    var validationResults = await validation.RunAsync(validationRequest, new(validationChoice.Provider), token);
                    if (validationResults.Count == 0)
                        LOGGER.LogWarning("Retrieval context validation returned no results. Continuing augmentation with all retrieved contexts.");
                    else
                    {
                        //
                        // Now, filter the retrieval contexts to the most relevant ones:
                        //
                        var validated = KeepFittingPassages(validationResults);
                        usedContexts = validated.Contexts;
                        LOGGER.LogInformation($"The check of which passages fit kept {validated.Contexts.Count:###,###,###,##0} of {validationResults.Count:###,###,###,##0} retrieved contexts: {validated.NumUnchecked:###,###,###,##0} without a usable judgment, {validated.NumRejected:###,###,###,##0} judged not to fit, {validated.NumBelowThreshold:###,###,###,##0} below the confidence threshold of {validated.Threshold:0.00}.");

                        //
                        // The user switched this check on. When it failed for every passage, the answer
                        // rests on unfiltered passages, just as when the check throws:
                        //
                        if (validated.IsUnchecked)
                        {
                            LOGGER.LogWarning("Retrieval context validation got no usable answer for any context. Continuing augmentation with all retrieved contexts.");
                            await WarnThatTheCheckFailedAsync();
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    LOGGER.LogError(exception, "Retrieval context validation failed. Continuing augmentation with all retrieved contexts.");

                    //
                    // The user switched this check on. Continuing without it silently would hide
                    // that the answer rests on unfiltered passages:
                    //
                    await WarnThatTheCheckFailedAsync();
                }
            }
            else
            {
                //
                // No message to the user here: which providers are trusted enough is a setting, not
                // an event. It does not change between two answers, so a message would repeat itself
                // with every single one until the setting changes.
                //
                LOGGER.LogWarning("Skipping retrieval context validation because no provider which is trusted enough is available for it. Continuing augmentation with all retrieved contexts.");
            }
        }

        //
        // Of what the check left, the model receives as much as its context window holds:
        //
        var selection = await SelectWithinBudgetAsync(provider.CreateSettingsProvider(chatModel), ByDataSource(retrievalContextsByDataSource, preparedContexts, usedContexts), guardService, reportingScope, token);
        LOGGER.LogInformation($"Starting the augmentation process over {selection.Contexts.Count:###,###,###,##0} of {retrievalContexts.Count:###,###,###,##0} retrieved contexts; {selection.LeftOutCount:###,###,###,##0} of them did not fit into what the model can read.");

        //
        // The user learns about it with every answer: each answer rests on the passages it got,
        // and each one may miss something else.
        //
        if (selection.LeftOutCount > 0)
            await MessageBus.INSTANCE.SendWarning(new(AppIcons.CONTENT_SHORTENED, string.Format(TA("Not every passage found in your data sources fits into what the selected model can read. This answer had to do without {0} of them. To give the AI every passage, switch on Semantic Search for this chat, lower the token limit of your data sources, or choose fewer data sources."), selection.LeftOutCount.CompactCount())));

        //
        // Add the augmented data to the chat thread, as the very last step: the caller restricts
        // the chat for these passages once this process returns, and a request whose RAG process
        // failed goes on without it, so nothing may fail between the two:
        //
        chatThread.AugmentedData = WriteAugmentedData(selection);
        return new(chatThread, selection.Contexts, selection.PassageCounts);
    }

    #endregion

    /// <summary>
    /// Keeps the passages which the check of which passages fit the user prompt let through.
    /// </summary>
    /// <remarks>
    /// A passage judged not to fit is left out, however confident the judgment. Of the passages judged to fit,
    /// those stay whose confidence reaches the threshold, which only they decide. A passage whose check failed
    /// stays: the failure says nothing about the passage, neither that it fits nor that it does not. It does not
    /// count for the threshold either, so that a failed check moves the line for none of the others.
    /// </remarks>
    /// <param name="validationResults">The results of the check, one for each passage.</param>
    /// <returns>The passages left, in the order of the results, and why the others were left out.</returns>
    internal static ValidatedPassages KeepFittingPassages(IReadOnlyList<RetrievalContextValidationResult> validationResults)
    {
        var numJudged = validationResults.Count(result => result.IsAnswered);
        var fitting = validationResults.Where(result => result.IsAnswered && result.Decision).ToList();

        //
        // Without a passage which fits, there is no threshold to find; asking for one would only log a warning:
        //
        var threshold = fitting.Count is 0 ? 0f : fitting.GetConfidenceThreshold(fitting.DetermineTargetWindow(TargetWindowStrategy.TOP10_BETTER_THAN_GUESSING));
        var contexts = validationResults.Where(result => !result.IsAnswered || (result.Decision && result.Confidence >= threshold)).Select(result => result.RetrievalContext).ToList();
        return new(contexts, numJudged, validationResults.Count - numJudged, numJudged - fitting.Count, fitting.Count(result => result.Confidence < threshold), threshold);
    }

    /// <summary>
    /// Tells the user that the check of which passages fit failed, so that the answer uses all passages found.
    /// </summary>
    private static Task WarnThatTheCheckFailedAsync() => MessageBus.INSTANCE.SendWarning(new(AppIcons.PASSAGE_RELEVANCE_CHECK, TA("The check of which passages fit your question failed. This answer uses all passages that were found.")));

    /// <summary>
    /// Writes what the model reads along with the user prompt: the passages which fit, and how many did not.
    /// </summary>
    /// <remarks>
    /// The model learns about the passages which were left out, so that it does not take what it
    /// read for everything the data sources hold on the user prompt.
    /// </remarks>
    /// <param name="selection">The passages which fit into the budget, and how many did not.</param>
    /// <returns>The augmented data, or nothing when there is nothing to tell.</returns>
    internal static string WriteAugmentedData(PassageSelection selection)
    {
        var sb = new StringBuilder();
        if (selection.Contexts.Count > 0)
        {
            sb.AppendLineLF("The following useful information will help you in processing the user prompt:");
            sb.AppendLineLF();

            // Let's convert the retrieval contexts to Markdown:
            for (var index = 0; index < selection.Contexts.Count; index++)
                sb.Append(selection.Contexts[index].ToMarkdown(index + 1, selection.Contexts.Count));
        }

        if (selection.LeftOutCount > 0)
            sb.AppendLineLF(selection.LeftOutCount is 1
                ? "Of the passages which the search found in the data sources, 1 did not fit into this request and was left out. An answer based on the data sources may therefore be incomplete."
                : $"Of the passages which the search found in the data sources, {selection.LeftOutCount} did not fit into this request and were left out. An answer based on the data sources may therefore be incomplete.");

        return sb.ToString();
    }

    /// <summary>
    /// Sorts the prepared contexts which the check of which passages fit left back to their data sources.
    /// </summary>
    /// <remarks>
    /// The contexts were prepared in one batch, in the order of their data sources, and the check
    /// hands back the very same objects. So each context left is found by reference, and keeps its
    /// rank within its data source, whichever order the check reported them in.
    /// </remarks>
    /// <param name="retrievalContextsByDataSource">The retrieval contexts per data source, as the retrieval issued them.</param>
    /// <param name="preparedContexts">All contexts, prepared in the same order.</param>
    /// <param name="usedContexts">The prepared contexts the check left.</param>
    /// <returns>The prepared contexts left of each data source, each in the order of their rank.</returns>
    private static IReadOnlyList<IReadOnlyList<PreparedRetrievalContext>> ByDataSource(IReadOnlyList<IReadOnlyList<IRetrievalContext>> retrievalContextsByDataSource, IReadOnlyList<PreparedRetrievalContext> preparedContexts, IReadOnlyList<PreparedRetrievalContext> usedContexts)
    {
        var used = new HashSet<PreparedRetrievalContext>(usedContexts, ReferenceEqualityComparer.Instance);
        var byDataSource = new List<IReadOnlyList<PreparedRetrievalContext>>(retrievalContextsByDataSource.Count);
        var first = 0;
        foreach (var contexts in retrievalContextsByDataSource)
        {
            byDataSource.Add(preparedContexts.Skip(first).Take(contexts.Count).Where(used.Contains).ToList());
            first += contexts.Count;
        }

        return byDataSource;
    }

    /// <summary>
    /// Selects the contexts which fit into the share of the model's context window the passages get, see PassageBudget.
    /// </summary>
    /// <remarks>
    /// The tokens are counted with the tokenizer of the provider, or the built-in one where none is
    /// configured, see ConversationTokenCounter. When the tokenizer does not answer, the characters
    /// are counted against the same limit instead: a token of prose holds several characters, and
    /// hardly ever less than one, so the passages then get less room rather than more.
    /// </remarks>
    /// <param name="settingsProvider">The settings of the provider and the model which answers.</param>
    /// <param name="contextsByDataSource">The prepared contexts of each data source, each in the order of their rank.</param>
    /// <param name="guardService">The prompt injection filter, for contexts which have to be shortened.</param>
    /// <param name="reportingScope">The action the contexts were retrieved for.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The contexts which fit, and how many did not.</returns>
    private static async Task<PassageSelection> SelectWithinBudgetAsync(AIStudio.Settings.Provider settingsProvider, IReadOnlyList<IReadOnlyList<PreparedRetrievalContext>> contextsByDataSource, PromptInjectionGuardService guardService, PromptInjectionReportingScope reportingScope, CancellationToken token)
    {
        var contextWindow = settingsProvider.GetModelProfile().Context;
        var budget = PassageBudget.For(contextWindow, CountTokensAsync);
        try
        {
            return await budget.SelectAsync(contextsByDataSource, guardService, reportingScope, token);
        }
        catch (Exception exception) when (contextWindow.IsKnown && exception is not OperationCanceledException)
        {
            LOGGER.LogWarning(exception, "Could not count the tokens of the retrieval contexts. Counting their characters against the same limit instead.");
            return await PassageBudget.InCharacters(budget.Limit).SelectAsync(contextsByDataSource, guardService, reportingScope, token);
        }

        Task<int> CountTokensAsync(string text, CancellationToken cancellationToken) => Program.SERVICE_PROVIDER.GetRequiredService<ConversationTokenCounter>().CountTextAsync(settingsProvider, text, cancellationToken);
    }
}