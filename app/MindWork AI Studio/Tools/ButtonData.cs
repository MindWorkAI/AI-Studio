namespace AIStudio.Tools;

public readonly record struct ButtonData(string Text, string Icon, Color Color, string Tooltip, Func<Task> AsyncAction, Func<bool>? DisabledActionParam) : IButtonData
{
    public ButtonTypes Type => ButtonTypes.BUTTON;
    
    public Func<bool> DisabledAction
    {
        get
        {
            var data = this;
            return () =>
            {
                if (data.DisabledActionParam is null)
                    return false;

                return data.DisabledActionParam();
            };
        }
    }
}