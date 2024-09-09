namespace AIStudio.Tools;

public static class SendToExtensions
{
    public static string Name(this Components assistant) => assistant switch
    {
        Components.GRAMMAR_SPELLING_ASSISTANT => "Grammar & Spelling Assistant",
        Components.TEXT_SUMMARIZER_ASSISTANT => "Text Summarizer Assistant",
        Components.ICON_FINDER_ASSISTANT => "Icon Finder Assistant",
        Components.TRANSLATION_ASSISTANT => "Translation Assistant",
        Components.REWRITE_ASSISTANT => "Rewrite Assistant",
        Components.AGENDA_ASSISTANT => "Agenda Assistant",
        Components.CODING_ASSISTANT => "Coding Assistant",
        Components.EMAIL_ASSISTANT => "E-Mail Assistant",
        Components.LEGAL_CHECK_ASSISTANT => "Legal Check Assistant",
        Components.SYNONYMS_ASSISTANT => "Synonym Assistant",
        Components.MY_TASKS_ASSISTANT => "My Tasks Assistant",
        
        Components.CHAT => "New Chat",
        
        _ => Enum.GetName(typeof(Components), assistant)!,
    };

    public static SendToData GetData(this Components destination) => destination switch
    {
        Components.AGENDA_ASSISTANT => new(Event.SEND_TO_AGENDA_ASSISTANT, Routes.ASSISTANT_AGENDA),
        Components.CODING_ASSISTANT => new(Event.SEND_TO_CODING_ASSISTANT, Routes.ASSISTANT_CODING),
        Components.REWRITE_ASSISTANT => new(Event.SEND_TO_REWRITE_ASSISTANT, Routes.ASSISTANT_REWRITE),
        Components.EMAIL_ASSISTANT => new(Event.SEND_TO_EMAIL_ASSISTANT, Routes.ASSISTANT_EMAIL),
        Components.TRANSLATION_ASSISTANT => new(Event.SEND_TO_TRANSLATION_ASSISTANT, Routes.ASSISTANT_TRANSLATION),
        Components.ICON_FINDER_ASSISTANT => new(Event.SEND_TO_ICON_FINDER_ASSISTANT, Routes.ASSISTANT_ICON_FINDER),
        Components.GRAMMAR_SPELLING_ASSISTANT => new(Event.SEND_TO_GRAMMAR_SPELLING_ASSISTANT, Routes.ASSISTANT_GRAMMAR_SPELLING),
        Components.TEXT_SUMMARIZER_ASSISTANT => new(Event.SEND_TO_TEXT_SUMMARIZER_ASSISTANT, Routes.ASSISTANT_SUMMARIZER),
        Components.LEGAL_CHECK_ASSISTANT => new(Event.SEND_TO_LEGAL_CHECK_ASSISTANT, Routes.ASSISTANT_LEGAL_CHECK),
        Components.SYNONYMS_ASSISTANT => new(Event.SEND_TO_SYNONYMS_ASSISTANT, Routes.ASSISTANT_SYNONYMS),
        Components.MY_TASKS_ASSISTANT => new(Event.SEND_TO_MY_TASKS_ASSISTANT, Routes.ASSISTANT_MY_TASKS),
        
        Components.CHAT => new(Event.SEND_TO_CHAT, Routes.CHAT),
        
        _ => new(Event.NONE, Routes.ASSISTANTS),
    };
}