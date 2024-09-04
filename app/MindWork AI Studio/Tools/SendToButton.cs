namespace AIStudio.Tools;

public readonly record struct SendToButton() : IButtonData
{
    public ButtonTypes Type => ButtonTypes.SEND_TO;

    public Func<string> GetText { get; init; } = () => string.Empty;
    
    public bool UseResultingContentBlockData { get; init; } = true;
    
    public Components Self { get; init; } = Components.NONE;

}