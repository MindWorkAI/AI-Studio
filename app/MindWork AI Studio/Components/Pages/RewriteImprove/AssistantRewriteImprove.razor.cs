using AIStudio.Tools;

namespace AIStudio.Components.Pages.RewriteImprove;

public partial class AssistantRewriteImprove : AssistantBaseCore
{
    protected override string Title => "Rewrite & Improve Text";
    
    protected override string Description =>
        """
        Rewrite and improve your text. Please note, that the capabilities of the different LLM providers will vary.
        """;
    
    protected override string SystemPrompt =>
        $"""
        You are an expert in language and style. You receive a text as input. First, you review the text. If no
        changes are needed, you return the text without modifications. If a change is necessary, you improve the
        text. You can also correct spelling and grammar errors. You never add additional information. You never
        ask the user for additional information. Your response only contains the improved text. You do not explain
        your changes. If no changes are needed, you return the text unchanged.
        The style of the text: {this.selectedWritingStyle.Prompt()}. You follow the rules according
        to {this.SystemPromptLanguage()} in all your changes.
        """;
    
    protected override bool ShowResult => false;

    #region Overrides of AssistantBase

    protected override bool ShowDedicatedProgress => true;

    #endregion

    protected override IReadOnlyList<ButtonData> FooterButtons => new[]
    {
        new ButtonData("Copy result", Icons.Material.Filled.ContentCopy, Color.Default, string.Empty, () => this.CopyToClipboard(this.rewrittenText)),
    };
    
    private string inputText = string.Empty;
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;
    private string rewrittenText = string.Empty;
    private WritingStyles selectedWritingStyle;
    
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
    
    private async Task RewriteText()
    {
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(this.inputText);
        
        this.rewrittenText = await this.AddAIResponseAsync(time);
        await this.JsRuntime.GenerateAndShowDiff(this.inputText, this.rewrittenText);
    }
}