using System.Text;

namespace AIStudio.Components.Pages.Coding;

public partial class AssistantCoding : AssistantBaseCore
{
    protected override string Title => "Coding Assistant";
    
    protected override string Description =>
        """
        This coding assistant supports you in writing code. Provide some coding context by copying and pasting
        your code into the input fields. You might assign an ID to your code snippet to easily reference it later.
        When you have compiler messages, you can paste them into the input fields to get help with debugging as well.
        """;
    
    protected override string SystemPrompt => 
        """
        You are a friendly, helpful senior software developer with extensive experience in various programming languages
        and concepts. You are familiar with principles like DRY, KISS, YAGNI, and SOLID and can apply and explain them.
        You know object-oriented programming, as well as functional programming and procedural programming. You are also
        familiar with design patterns and can explain them. You are an expert of debugging and can help with compiler
        messages. You can also help with code refactoring and optimization.
        
        When the user asks in a different language than English, you answer in the same language!
        """;
    
    private readonly List<CodingContext> codingContexts = new();
    private bool provideCompilerMessages;
    private string compilerMessages = string.Empty;
    private string questions = string.Empty;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        if (this.SettingsManager.ConfigurationData.Coding.PreselectOptions)
        {
            this.provideCompilerMessages = this.SettingsManager.ConfigurationData.Coding.PreselectCompilerMessages;
            this.providerSettings = this.SettingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.SettingsManager.ConfigurationData.Coding.PreselectedProvider);
        }
        
        await base.OnInitializedAsync();
    }

    #endregion

    private string? ValidatingCompilerMessages(string checkCompilerMessages)
    {
        if(!this.provideCompilerMessages)
            return null;
        
        if(string.IsNullOrWhiteSpace(checkCompilerMessages))
            return "Please provide the compiler messages.";
        
        return null;
    }
    
    private string? ValidateQuestions(string checkQuestions)
    {
        if(string.IsNullOrWhiteSpace(checkQuestions))
            return "Please provide your questions.";
        
        return null;
    }

    private void AddCodingContext()
    {
        this.codingContexts.Add(new()
        {
            Id = $"Context {this.codingContexts.Count + 1}",
            Language = this.SettingsManager.ConfigurationData.Coding.PreselectOptions ? this.SettingsManager.ConfigurationData.Coding.PreselectedProgrammingLanguage : default,
            OtherLanguage = this.SettingsManager.ConfigurationData.Coding.PreselectOptions ? this.SettingsManager.ConfigurationData.Coding.PreselectedOtherProgrammingLanguage : string.Empty,
        });
    }

    private async Task GetSupport()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;

        var sbContext = new StringBuilder();
        if (this.codingContexts.Count > 0)
        {
            sbContext.AppendLine("I have the following coding context:");
            sbContext.AppendLine();
            foreach (var codingContext in this.codingContexts)
            {
                sbContext.AppendLine($"ID: {codingContext.Id}");
                sbContext.AppendLine($"Language: {codingContext.Language.Name()}");
                sbContext.AppendLine($"Other Language: {codingContext.OtherLanguage}");
                sbContext.AppendLine($"Content:");
                sbContext.AppendLine("```");
                sbContext.AppendLine(codingContext.Code);
                sbContext.AppendLine("```");
                sbContext.AppendLine();
            }
        }

        var sbCompilerMessages = new StringBuilder();
        if (this.provideCompilerMessages)
        {
            sbCompilerMessages.AppendLine("I have the following compiler messages:");
            sbCompilerMessages.AppendLine();
            sbCompilerMessages.AppendLine("```");
            sbCompilerMessages.AppendLine(this.compilerMessages);
            sbCompilerMessages.AppendLine("```");
            sbCompilerMessages.AppendLine();
        }

        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                {sbContext}
                {sbCompilerMessages}
                
                My questions are:
                {this.questions}
             """);

        await this.AddAIResponseAsync(time);
    }
}