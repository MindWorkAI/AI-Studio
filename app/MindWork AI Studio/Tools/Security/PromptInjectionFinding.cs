namespace AIStudio.Tools.Security;

public sealed record PromptInjectionFinding(string RuleId, string Category, string DetectionStage, string Snippet);