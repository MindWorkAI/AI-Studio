namespace AIStudio.Components.Pages;

public static class SendToAssistantExtensions
{
    public static string Name(this SendToAssistant assistant)
    {
        return assistant switch
        {
            SendToAssistant.GRAMMAR_SPELLING_ASSISTANT => "Grammar & Spelling Assistant",
            SendToAssistant.TEXT_SUMMARIZER_ASSISTANT => "Text Summarizer Assistant",
            SendToAssistant.ICON_FINDER_ASSISTANT => "Icon Finder Assistant",
            SendToAssistant.TRANSLATION_ASSISTANT => "Translation Assistant",
            SendToAssistant.REWRITE_ASSISTANT => "Rewrite Assistant",
            SendToAssistant.AGENDA_ASSISTANT => "Agenda Assistant",
            SendToAssistant.CODING_ASSISTANT => "Coding Assistant",
            
            _ => "Send to ...",
        };
    }
}