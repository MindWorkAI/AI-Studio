using System.Reflection;
using System.Runtime.CompilerServices;

using AIStudio.Assistants;
using AIStudio.Assistants.Agenda;
using AIStudio.Assistants.BatchProcessing;
using AIStudio.Assistants.BiasDay;
using AIStudio.Assistants.Builder;
using AIStudio.Assistants.Coding;
using AIStudio.Assistants.DocumentAnalysis;
using AIStudio.Assistants.Dynamic;
using AIStudio.Assistants.EMail;
using AIStudio.Assistants.ERI;
using AIStudio.Assistants.GrammarSpelling;
using AIStudio.Assistants.I18N;
using AIStudio.Assistants.IconFinder;
using AIStudio.Assistants.JobPosting;
using AIStudio.Assistants.LegalCheck;
using AIStudio.Assistants.MyTasks;
using AIStudio.Assistants.PromptOptimizer;
using AIStudio.Assistants.RewriteImprove;
using AIStudio.Assistants.SlideBuilder;
using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Assistants.Synonym;
using AIStudio.Assistants.TextSummarizer;
using AIStudio.Assistants.Translation;
using AIStudio.Tools.AssistantLaunch;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Holds which system prompt a chat gets that continues the conversation of each assistant.
/// </summary>
/// <remarks>
/// Every assistant decides this itself, and the compiler makes sure that each one does. This table keeps
/// what was decided, so that a change shows up as a change of this table, not as a side effect of
/// another change: in v26.4.1, moving the default prompt into the common path took the prompts of
/// several assistants away without anybody noticing. A new assistant fails the coverage test until it
/// is listed here. Assistant plugins choose in their Lua file, which their own tests cover.<br/><br/>
///
/// The assistants are created without running their constructors, because only their choice is read,
/// and building a prompt of their own for the chat would need their state.
/// </remarks>
[TestFixture]
public sealed class ChatSystemPromptOfAssistantsTests
{
    private static readonly Assembly APP = typeof(AssistantLaunchService).Assembly;

    private static readonly Dictionary<Type, ChatSystemPromptSource> DECISIONS = new()
    {
        [typeof(AssistantAgenda)] = ChatSystemPromptSource.CUSTOM,
        [typeof(AssistantBatchProcessing)] = ChatSystemPromptSource.AI_STUDIO_DEFAULT,
        [typeof(BiasOfTheDayAssistant)] = ChatSystemPromptSource.ASSISTANT,
        [typeof(AssistantBuilder)] = ChatSystemPromptSource.AI_STUDIO_DEFAULT,
        [typeof(AssistantCoding)] = ChatSystemPromptSource.ASSISTANT,
        [typeof(DocumentAnalysisAssistant)] = ChatSystemPromptSource.AI_STUDIO_DEFAULT,
        [typeof(AssistantEMail)] = ChatSystemPromptSource.CUSTOM,
        [typeof(AssistantERI)] = ChatSystemPromptSource.ASSISTANT,
        [typeof(AssistantGrammarSpelling)] = ChatSystemPromptSource.CUSTOM,
        [typeof(AssistantI18N)] = ChatSystemPromptSource.AI_STUDIO_DEFAULT,
        [typeof(AssistantIconFinder)] = ChatSystemPromptSource.ASSISTANT,
        [typeof(AssistantJobPostings)] = ChatSystemPromptSource.CUSTOM,
        [typeof(AssistantLegalCheck)] = ChatSystemPromptSource.ASSISTANT,
        [typeof(AssistantMyTasks)] = ChatSystemPromptSource.ASSISTANT,
        [typeof(AssistantPromptOptimizer)] = ChatSystemPromptSource.AI_STUDIO_DEFAULT,
        [typeof(AssistantRewriteImprove)] = ChatSystemPromptSource.CUSTOM,
        [typeof(AssistantSWOTAnalysis)] = ChatSystemPromptSource.CUSTOM,
        [typeof(SlideAssistant)] = ChatSystemPromptSource.AI_STUDIO_DEFAULT,
        [typeof(AssistantSynonyms)] = ChatSystemPromptSource.CUSTOM,
        [typeof(AssistantTextSummarizer)] = ChatSystemPromptSource.CUSTOM,
        [typeof(AssistantTranslation)] = ChatSystemPromptSource.CUSTOM,
    };

    private static IEnumerable<Type> Assistants() => DECISIONS.Keys;

    [TestCaseSource(nameof(Assistants))]
    public void TheAssistantKeepsItsDecision(Type assistant) => Assert.That(SourceOf(assistant), Is.EqualTo(DECISIONS[assistant]));

    [Test]
    public void EveryAssistantIsListed()
    {
        var assistants = APP.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && IsAssistant(type))
            .Where(type => type != typeof(AssistantDynamic))
            .ToList();

        Assert.That(assistants, Is.EquivalentTo(DECISIONS.Keys), "Each assistant needs its decision in this table.");
    }

    private static ChatSystemPromptSource SourceOf(Type assistant)
    {
        var instance = RuntimeHelpers.GetUninitializedObject(assistant);
        var property = assistant.GetProperty("ChatSystemPrompt", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((ChatSystemPromptChoice)property.GetValue(instance)!).Source;
    }

    private static bool IsAssistant(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AssistantBase<>))
                return true;

        return false;
    }
}