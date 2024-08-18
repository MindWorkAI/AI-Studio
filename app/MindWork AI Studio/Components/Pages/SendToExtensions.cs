namespace AIStudio.Components.Pages;

public static class SendToExtensions
{
    public static string Name(this SendTo assistant)
    {
        return assistant switch
        {
            SendTo.GRAMMAR_SPELLING_ASSISTANT => "Grammar & Spelling Assistant",
            SendTo.TEXT_SUMMARIZER_ASSISTANT => "Text Summarizer Assistant",
            SendTo.ICON_FINDER_ASSISTANT => "Icon Finder Assistant",
            SendTo.TRANSLATION_ASSISTANT => "Translation Assistant",
            SendTo.REWRITE_ASSISTANT => "Rewrite Assistant",
            SendTo.AGENDA_ASSISTANT => "Agenda Assistant",
            SendTo.CODING_ASSISTANT => "Coding Assistant",
            
            _ => "Send to ...",
        };
    }
}