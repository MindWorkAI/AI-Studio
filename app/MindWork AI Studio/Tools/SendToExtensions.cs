namespace AIStudio.Tools;

public static class SendToExtensions
{
    public static string Name(this SendTo assistant) => assistant switch
    {
        SendTo.GRAMMAR_SPELLING_ASSISTANT => "Grammar & Spelling Assistant",
        SendTo.TEXT_SUMMARIZER_ASSISTANT => "Text Summarizer Assistant",
        SendTo.ICON_FINDER_ASSISTANT => "Icon Finder Assistant",
        SendTo.TRANSLATION_ASSISTANT => "Translation Assistant",
        SendTo.REWRITE_ASSISTANT => "Rewrite Assistant",
        SendTo.AGENDA_ASSISTANT => "Agenda Assistant",
        SendTo.CODING_ASSISTANT => "Coding Assistant",
        SendTo.EMAIL_ASSISTANT => "E-Mail Assistant",
        SendTo.LEGAL_CHECK_ASSISTANT => "Legal Check Assistant",
        
        SendTo.CHAT => "New Chat",
        
        _ => "Send to ...",
    };

    public static SendToData GetData(this SendTo destination) => destination switch
    {
        SendTo.AGENDA_ASSISTANT => new(Event.SEND_TO_AGENDA_ASSISTANT, Routes.ASSISTANT_AGENDA),
        SendTo.CODING_ASSISTANT => new(Event.SEND_TO_CODING_ASSISTANT, Routes.ASSISTANT_CODING),
        SendTo.REWRITE_ASSISTANT => new(Event.SEND_TO_REWRITE_ASSISTANT, Routes.ASSISTANT_REWRITE),
        SendTo.EMAIL_ASSISTANT => new(Event.SEND_TO_EMAIL_ASSISTANT, Routes.ASSISTANT_EMAIL),
        SendTo.TRANSLATION_ASSISTANT => new(Event.SEND_TO_TRANSLATION_ASSISTANT, Routes.ASSISTANT_TRANSLATION),
        SendTo.ICON_FINDER_ASSISTANT => new(Event.SEND_TO_ICON_FINDER_ASSISTANT, Routes.ASSISTANT_ICON_FINDER),
        SendTo.GRAMMAR_SPELLING_ASSISTANT => new(Event.SEND_TO_GRAMMAR_SPELLING_ASSISTANT, Routes.ASSISTANT_GRAMMAR_SPELLING),
        SendTo.TEXT_SUMMARIZER_ASSISTANT => new(Event.SEND_TO_TEXT_SUMMARIZER_ASSISTANT, Routes.ASSISTANT_SUMMARIZER),
        SendTo.LEGAL_CHECK_ASSISTANT => new(Event.SEND_TO_LEGAL_CHECK_ASSISTANT, Routes.ASSISTANT_LEGAL_CHECK),
            
        SendTo.CHAT => new(Event.SEND_TO_CHAT, Routes.CHAT),
            
        _ => new(Event.NONE, Routes.ASSISTANTS),
    };
}