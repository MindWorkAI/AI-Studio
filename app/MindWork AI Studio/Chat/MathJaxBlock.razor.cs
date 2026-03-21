using Microsoft.AspNetCore.Components;

namespace AIStudio.Chat;

public partial class MathJaxBlock
{
    [Parameter]
    public string Value { get; init; } = string.Empty;

    [Parameter]
    public string Class { get; init; } = string.Empty;

    private string RootClass => string.IsNullOrWhiteSpace(this.Class)
        ? "chat-mathjax-block"
        : $"chat-mathjax-block {this.Class}";

    private string MathText => $"$${Environment.NewLine}{this.Value}{Environment.NewLine}$$";
}