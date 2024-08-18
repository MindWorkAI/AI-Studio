using AIStudio.Components.Pages;

namespace AIStudio.Tools;

public readonly record struct SendToButton() : IButtonData
{
    public ButtonTypes Type => ButtonTypes.SEND_TO;

    public Func<string> GetData { get; init; } = () => string.Empty;
    
    public bool UseResultingContentBlockData { get; init; } = true;
    
    public SendToAssistant Self { get; init; } = SendToAssistant.NONE;

}