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
        You are an expert in languages and their rules. For example, you know how US and UK English or German in
        Germany and German in Austria differ. You receive text as input. You check the spelling and grammar of
        this text according to the rules of {this.SystemPromptLanguage()}. You never add information. You
        never ask the user for additional information. You do not attempt to improve the wording of the text.
        Your response includes only the corrected text. Do not explain your changes. If no changes are needed,
        you return the text unchanged.
        """;

    protected override bool ShowResult => false;
    
    protected override IReadOnlyList<ButtonData> FooterButtons => new[]
    {
        new ButtonData("Copy corrected text", Icons.Material.Filled.ContentCopy, Color.Default, string.Empty, this.CopyToClipboard),
    };

    private string inputText = string.Empty;
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;
    private string correctedText = string.Empty;

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
            CommonLanguages.AS_IS => "the source language",
            CommonLanguages.OTHER => this.customTargetLanguage,
            
            _ => $"{this.selectedTargetLanguage.Name()}",
        };

        if (string.IsNullOrWhiteSpace(lang))
            return "the source language";

        return lang;
    }

    private async Task ProofreadText()
    {
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(this.inputText);
        
        this.correctedText = await this.AddAIResponseAsync(time);
        await this.JsRuntime.GenerateAndShowDiff(this.inputText, this.correctedText);
    }
    
    private async Task CopyToClipboard()
    {
        await this.Rust.CopyText2Clipboard(this.JsRuntime, this.Snackbar, this.correctedText);
    }
}