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
        """;
    
    private readonly List<CodingContext> codingContexts = new();
}