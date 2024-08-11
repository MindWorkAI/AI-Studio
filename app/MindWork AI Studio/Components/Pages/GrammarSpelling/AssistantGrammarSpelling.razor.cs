using AIStudio.Tools;

namespace AIStudio.Components.Pages.GrammarSpelling;

public partial class AssistantGrammarSpelling : AssistantBaseCore
{
    protected override string Title => "Grammar and Spelling Checker";
    
    protected override string Description =>
        """
        Check the grammar and spelling of a text.
        """;
    
    protected override string SystemPrompt => 
        $"""
        You get a text as input. The user wants you to check the grammar and spelling of the text.
        Correct any spelling or grammar mistakes. Do not add any information. Do not ask for additional information.
        Do not improve the text. Do not mirror the user's language. Do not mirror your task.{this.SystemPromptLanguage()}
        """;

    protected override bool ShowResult => true;

    private string originalText = string.Empty;
    private string inputText = string.Empty;
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;

    private string? ValidateText(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return "Please provide a text as input. You might copy the desired text from a document or a website.";
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return "Please provide a custom language.";
        
        return null;
    }
    
    private string SystemPromptLanguage()
    {
        var lang = this.selectedTargetLanguage switch
        {
            CommonLanguages.AS_IS => string.Empty,
            CommonLanguages.OTHER => this.customTargetLanguage,
            
            _ => this.selectedTargetLanguage.Name(),
        };

        if (string.IsNullOrWhiteSpace(lang))
            return string.Empty;
        
        return
            $"""
             The text is written in {lang}.
            """;
    }

    private async Task ProofreadText()
    {
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(this.inputText);
        
        await this.AddAIResponseAsync(time);
    }
}