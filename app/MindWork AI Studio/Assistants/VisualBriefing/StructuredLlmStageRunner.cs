using System.Diagnostics;
using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Implements structured model stages on the existing provider and hidden-chat primitives.
/// </summary>
internal sealed class StructuredLlmStageRunner(
    ILogger<StructuredLlmStageRunner> logger)
{
    /// <summary>
    /// Runs one structured model stage with exactly one same-context repair attempt.
    /// </summary>
    /// <typeparam name="T">The strict response type.</typeparam>
    /// <param name="provider">The selected provider configuration.</param>
    /// <param name="profile">The selected user profile.</param>
    /// <param name="systemContract">The stage-specific system contract.</param>
    /// <param name="prompt">The user prompt containing stage inputs.</param>
    /// <param name="attachments">The first-turn attachments.</param>
    /// <param name="stage">The build stage.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="buildId">The build identifier.</param>
    /// <param name="validate">Strict semantic validation for a parsed response.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The validated stage result.</returns>
    public async Task<StructuredLlmStageResult<T>> RunAsync<T>(
        ProviderSettings provider,
        Profile profile,
        string systemContract,
        string prompt,
        IReadOnlyList<FileAttachment> attachments,
        VisualBriefingBuildStage stage,
        Guid operationId,
        Guid buildId,
        Func<T, VisualBriefingContractIssue?> validate,
        CancellationToken token)
        where T : class
    {
        var systemPrompt = $"""
                            {systemContract}

                            {VisualBriefingStructuredResponseProcessor.BuildContractGrammar<T>()}

                            JSON transport rules:
                            Use standard JSON with double-quoted property names and string values.
                            Escape quotation marks, backslashes, line breaks, tabs, and other control characters inside strings.
                            Do not use comments, trailing commas, ellipses, or unescaped multiline strings.
                            Use compact JSON and concise, non-redundant string values so the complete root object fits in the response.
                            Before sending, silently verify that the root object is closed and every property conforms to the grammar.
                            Answer with the bare JSON object and nothing else: no explanation, no Markdown, and no code fence.

                            User profile:
                            {profile.ToSystemPrompt()}
                            """;
        
        var time = DateTimeOffset.UtcNow;
        var initialPrompt = new ContentText
        {
            Text = prompt,
            FileAttachments = [.. attachments],
        };
        
        var thread = new ChatThread
        {
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = $"Visual Briefing {stage}",
            SystemPrompt = systemPrompt,
            SelectedProvider = provider.Id,
            Blocks =
            [
                CreateBlock(time, ChatRole.USER, initialPrompt),
            ],
        };

        VisualBriefingContractIssue? repairIssue = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var input = attempt == 1
                ? initialPrompt
                : new ContentText
                {
                    Text = BuildRepairPrompt(repairIssue!),
                };
            
            if (attempt == 2)
                thread.Blocks.Add(CreateBlock(DateTimeOffset.UtcNow, ChatRole.USER, input));

            var aiText = new ContentText { InitialRemoteWait = true };
            thread.Blocks.Add(CreateBlock(DateTimeOffset.UtcNow, ChatRole.AI, aiText));
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await aiText.CreateFromProviderAsync(
                    provider.CreateProvider(),
                    provider.Model,
                    input,
                    thread,
                    token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    Event(VisualBriefingLogEventId.VALIDATION_REJECTED),
                    "Visual briefing provider call failed. OperationId={OperationId} BuildId={BuildId} Stage={Stage} ProviderFamily={ProviderFamily} Model={Model} Attempt={Attempt} ExceptionType={ExceptionType}",
                    operationId,
                    buildId,
                    stage,
                    provider.UsedLLMProvider,
                    provider.Model,
                    attempt,
                    exception.GetType().Name);
                
                throw new VisualBriefingBuildException(
                    VisualBriefingFailureCode.PROVIDER_CALL_FAILED,
                    stage,
                    "The selected model provider could not complete this briefing stage.",
                    $"ProviderFamily={provider.UsedLLMProvider}; Model={provider.Model}; Attempt={attempt}; ExceptionType={exception.GetType().Name}.");
            }
            
            stopwatch.Stop();
            
            var answer = aiText.Text;
            logger.LogInformation(
                Event(stage is VisualBriefingBuildStage.DESIGN
                    ? VisualBriefingLogEventId.DESIGN_CALL_FINISHED
                    : VisualBriefingLogEventId.STRUCTURED_CALL_FINISHED),
                "Visual briefing model call finished. OperationId={OperationId} BuildId={BuildId} Stage={Stage} ProviderFamily={ProviderFamily} Model={Model} Attempt={Attempt} DurationMs={DurationMs} ResponseLength={ResponseLength}",
                operationId,
                buildId,
                stage,
                provider.UsedLLMProvider,
                provider.Model,
                attempt,
                stopwatch.ElapsedMilliseconds,
                answer.Length);

            var processing = VisualBriefingStructuredResponseProcessor.Process(answer, validate);
            var parsed = processing.Response;
            var issue = processing.Issue;
            
            if (issue is null)
            {
                if (parsed is null)
                    throw new UnreachableException();
                
                if (attempt == 2)
                    logger.LogInformation(
                        Event(VisualBriefingLogEventId.REPAIR_FINISHED),
                        "Visual briefing same-context repair finished. OperationId={OperationId} BuildId={BuildId} Stage={Stage}",
                        operationId,
                        buildId,
                        stage);
                
                return new(
                    true,
                    parsed,
                    string.Empty,
                    VisualBriefingFailureCode.NONE,
                    VisualBriefingValidationRule.NONE,
                    null,
                    attempt,
                    answer.Length);
            }

            // VisualBriefingStructuredResponseProcessor always supplies a diagnostic:
            var diagnostic = issue.Diagnostic!;
            logger.LogWarning(
                Event(VisualBriefingLogEventId.VALIDATION_REJECTED),
                "Visual briefing structured response rejected. OperationId={OperationId} BuildId={BuildId} Stage={Stage} Attempt={Attempt} FailureCode={FailureCode} ValidationRule={ValidationRule} StructuredIssue={StructuredIssue} Envelope={Envelope} CandidateIndex={CandidateIndex} CandidateCount={CandidateCount} JsonPath={JsonPath} Line={Line} BytePositionInLine={BytePositionInLine} Field={Field} Expected={Expected} ResponseLength={ResponseLength} Issue={Issue}",
                operationId,
                buildId,
                stage,
                attempt,
                issue.Code,
                issue.Rule,
                diagnostic.IssueKind,
                diagnostic.Envelope,
                diagnostic.CandidateIndex,
                diagnostic.CandidateCount,
                diagnostic.JsonPath,
                diagnostic.LineNumber,
                diagnostic.BytePositionInLine,
                diagnostic.FieldName,
                diagnostic.Expected,
                answer.Length,
                issue.Issue);
            
            if (attempt == 2)
                return new(
                    false,
                    null,
                    issue.Issue,
                    issue.Code,
                    issue.Rule,
                    diagnostic,
                    attempt,
                    answer.Length);

            logger.LogInformation(
                Event(VisualBriefingLogEventId.REPAIR_STARTED),
                "Visual briefing same-context repair started. OperationId={OperationId} BuildId={BuildId} Stage={Stage} FailureCode={FailureCode} ValidationRule={ValidationRule} StructuredIssue={StructuredIssue} JsonPath={JsonPath} Expected={Expected} Issue={Issue}",
                operationId,
                buildId,
                stage,
                issue.Code,
                issue.Rule,
                diagnostic.IssueKind,
                diagnostic.JsonPath,
                diagnostic.Expected,
                issue.Issue);
            repairIssue = issue;
        }

        throw new UnreachableException();
    }

    /// <summary>
    /// Creates a hidden chat block for a structured stage.
    /// </summary>
    /// <param name="time">The block time.</param>
    /// <param name="role">The chat role.</param>
    /// <param name="content">The text content.</param>
    /// <returns>The hidden chat block.</returns>
    private static ContentBlock CreateBlock(DateTimeOffset time, ChatRole role, ContentText content) => new()
    {
        Time = time,
        ContentType = ContentType.TEXT,
        Role = role,
        Content = content,
        HideFromUser = true,
    };

    /// <summary>
    /// Creates a precise provider-neutral repair instruction.
    /// </summary>
    /// <param name="issue">The safe rejection of the preceding assistant response.</param>
    /// <returns>The repair prompt without copied model or user content.</returns>
    private static string BuildRepairPrompt(VisualBriefingContractIssue issue)
    {
        var diagnostic = issue.Diagnostic;
        var location = diagnostic is null
            ? string.Empty
            : $"""
               Structural issue: {diagnostic.IssueKind}
               Candidate envelope: {diagnostic.Envelope}
               Candidate: {diagnostic.CandidateIndex} of {diagnostic.CandidateCount}
               JSON path: {diagnostic.JsonPath}
               Response line: {diagnostic.LineNumber?.ToString() ?? "unknown"}
               Byte position in line: {diagnostic.BytePositionInLine?.ToString() ?? "unknown"}
               Unknown or missing field: {(string.IsNullOrEmpty(diagnostic.FieldName) ? "none" : diagnostic.FieldName)}
               Expected shape: {(string.IsNullOrEmpty(diagnostic.Expected) ? "the active contract" : diagnostic.Expected)}
               """;
        
        var truncation = diagnostic?.IssueKind is VisualBriefingStructuredResponseIssueKind.UNEXPECTED_END
            ? "The preceding response ended before the root object was closed. Regenerate it completely and shorten non-essential prose values if necessary."
            : string.Empty;
        
        return $"""
                Correct the complete preceding assistant response so it satisfies the same strict contract.
                The preceding assistant response is the rejected response; do not ask for it again and do not return a patch.
                Return the entire corrected JSON object without explanation. Do not repeat the source material.
                Validation code: {issue.Code}
                Validation rule: {issue.Rule}
                Validation issue: {issue.Issue}
                {location}
                {truncation}
                """;
    }

    /// <summary>
    /// Creates a logging event from a stable visual briefing event identifier.
    /// </summary>
    /// <param name="eventId">The stable event identifier.</param>
    /// <returns>The logging event.</returns>
    private static EventId Event(VisualBriefingLogEventId eventId) => new((int)eventId, eventId.ToString());
}