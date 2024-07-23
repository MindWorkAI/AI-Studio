using AIStudio.Tools;

namespace AIStudio.Components.Pages.TextSummarizer;

public partial class AssistantTextSummarizer : AssistantBaseCore
{
    protected override string Title => "Text Summarizer";
    
    protected override string Description =>
        """
        Summarize long text into a shorter version while retaining the main points.
        You might want to change the language of the summary to make it more readable.
        It is also possible to change the complexity of the summary to make it
        easy to understand.
        """;
    
    protected override string SystemPrompt => 
        """
        You get a long text as input. The user wants to get a summary of the text.
        The user might want to change the language of the summary. In this case,
        you should provide a summary in the requested language. Eventually, the user
        want to change the complexity of the text. In this case, you should provide
        a summary with the requested complexity. In any case, do not add any information.
        """;
    
    private string inputText = string.Empty;
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;
    private Complexity selectedComplexity;
    private string expertInField = string.Empty;

    private string? ValidatingText(string text)
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
    
    private string? ValidateExpertInField(string field)
    {
        if(this.selectedComplexity == Complexity.SCIENTIFIC_LANGUAGE_OTHER_EXPERTS && string.IsNullOrWhiteSpace(field))
            return "Please provide your field of expertise.";
        
        return null;
    }

    private async Task SummarizeText()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                {this.selectedTargetLanguage.Prompt(this.customTargetLanguage)}
                {this.selectedComplexity.Prompt(this.expertInField)}
                
                Please summarize the following text:
                
                ```
                {this.inputText}
                ```
             """);

        await this.AddAIResponseAsync(time);
    }
}