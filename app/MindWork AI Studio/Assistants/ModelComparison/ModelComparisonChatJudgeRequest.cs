namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the judge of a comparison series is asked: the two chats of a task, as transcripts, see ChatTranscript.
/// </summary>
/// <remarks>
/// The judge is as blind as the user: the chats go in as "Chat 1" and "Chat 2", the names the user sees, never by
/// model. It answers in the same JSON as the judge of the stability mode, see ModelComparisonJudgeFormat.CHATS.
/// </remarks>
/// <param name="TranscriptOfChatOne">The first chat.</param>
/// <param name="TranscriptOfChatTwo">The second chat.</param>
/// <param name="LanguageName">The language of the reasoning, by its English name, or an empty text to leave it to the judge.</param>
/// <param name="Expectations">
/// The expectations, values, and goals of the user, by which the judge decides, see ModelComparisonJudgeExpectations;
/// or an empty text, when the user confirmed that the judge may decide by its own standards.
/// </param>
public sealed record ModelComparisonChatJudgeRequest(string TranscriptOfChatOne, string TranscriptOfChatTwo, string LanguageName, string Expectations)
{
    /// <summary>
    /// Builds the prompt for the judge model, asking for one JSON object and nothing else.
    /// </summary>
    /// <param name="cacheBuster">An identifier against caching gateways, see ModelComparisonJudgeFormat.WithCacheBuster.</param>
    /// <returns>The user prompt the judge model receives.</returns>
    public string ToPrompt(string cacheBuster = "")
    {
        var prompt = $$"""
                       You judge two chats in which a user worked on the same task, each time with another AI model. Both chats began with the same first message; afterward, the user may have written different messages in each. You do not know, and must not guess, which model answered in which chat, so judge only by what the chats say.

                       Judge in which chat the AI model helped the user more with the task: how correct, complete, and useful its answers were, and how little the user had to correct it or ask again. A call of a tool is a line of its own, without its result. Attached files are named, without their content.{{ModelComparisonJudgeFormat.ExpectationsOfTheUser(this.Expectations)}}

                       # Chat 1
                       {{this.TranscriptOfChatOne}}

                       # Chat 2
                       {{this.TranscriptOfChatTwo}}

                       Decide in which chat the AI model helped more, or whether both helped equally. {{ModelComparisonJudgeFormat.CHATS.AnswerInstructions(this.LanguageName)}}
                       """;

        return ModelComparisonJudgeFormat.WithCacheBuster(prompt, cacheBuster);
    }
}