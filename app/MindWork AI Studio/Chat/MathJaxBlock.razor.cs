using Microsoft.AspNetCore.Components;

namespace AIStudio.Chat;

public partial class MathJaxBlock
{
    private const string MATH_JAX_SCRIPT_ID = "mudblazor-markdown-mathjax";

    [Parameter]
    public string Value { get; init; } = string.Empty;

    [Parameter]
    public string Class { get; init; } = string.Empty;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    private string RootClass => string.IsNullOrWhiteSpace(this.Class)
        ? "chat-mathjax-block"
        : $"chat-mathjax-block {this.Class}";

    private string MathText => $"$${Environment.NewLine}{this.Value}{Environment.NewLine}$$";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await this.JsRuntime.InvokeVoidAsync("appendMathJaxScript", MATH_JAX_SCRIPT_ID);
        await this.JsRuntime.InvokeVoidAsync("refreshMathJaxScript");
        await base.OnAfterRenderAsync(firstRender);
    }
}