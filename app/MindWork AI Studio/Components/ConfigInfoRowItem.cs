namespace AIStudio.Components;

public sealed record ConfigInfoRowItem(
    string Icon,
    string Text,
    string CopyValue,
    string CopyTooltip,
    string Style = ""
);