namespace AIStudio.Tools;

public readonly record struct ButtonData(string Text, string Icon, Color Color, string Tooltip, Func<Task> AsyncAction) : IButtonData
{
    public ButtonTypes Type => ButtonTypes.BUTTON;
}