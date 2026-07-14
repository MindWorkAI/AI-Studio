namespace AIStudio.Tools.Security;

public sealed class PromptInjectionBlockedException(PromptInjectionScanResult result, string message) : Exception(message)
{
    public PromptInjectionScanResult Result { get; } = result;
}