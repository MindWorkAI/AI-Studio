using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class CodeEditor : ComponentBase, IAsyncDisposable
{
    private static readonly CodeEditorTheme DARK_CODE_EDITOR_THEME = new("#191a1c", "#bdbdbd", "#404040", "#85c46c", "#c9a26d", "#ed94c0", "#6c95eb", "#39cc9b", "#66c3cc");
    private static readonly CodeEditorTheme LIGHT_CODE_EDITOR_THEME = new("#fefcf6", "#383838", "#d8d8d8", "#248700", "#8c6c41", "#ab2f6b", "#0f54d6", "#00855f", "#0093a1");

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    [Inject]
    private global::AIStudio.Settings.SettingsManager SettingsManager { get; init; } = null!;

    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public CodeEditorLanguage Language { get; set; } = CodeEditorLanguage.PLAIN_TEXT;

    [Parameter] 
    public string Class { get; set; } = string.Empty;

    private readonly string editorId = $"code-editor-{Guid.NewGuid():N}";
    private const string CODE_EDITOR_MODULE = "./system/CodeEditor/code-editor.js?v=20260713-1";
    private ElementReference editorElement;
    private ElementReference lineNumbersElement;
    private IJSObjectReference? module;
    private string CodeEditorThemeStyle => this.GetCodeEditorThemeStyle();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        
        this.module = await this.JsRuntime.InvokeAsync<IJSObjectReference>("import", CODE_EDITOR_MODULE);
        await this.module.InvokeVoidAsync("init", this.editorId, this.editorElement, this.lineNumbersElement, this.Value, this.Language.ToString());
    }

    public async ValueTask<string> GetCodeAsync()
    {
        if (this.module is null)
            return this.Value;

        return await this.module.InvokeAsync<string>("getCode", this.editorId);
    }

    public async ValueTask SetCodeAsync(string code)
    {
        this.Value = code;
        if (this.module is null)
            return;

        await this.module.InvokeVoidAsync("setCode", this.editorId, code);
    }

    private string GetCodeEditorThemeStyle()
    {
        var codeEditorTheme = this.SettingsManager.IsDarkMode ? DARK_CODE_EDITOR_THEME : LIGHT_CODE_EDITOR_THEME;

        return
            $"--mw-code-editor-background: {codeEditorTheme.Background}; " +
            $"--mw-code-editor-foreground: {codeEditorTheme.Foreground}; " +
            $"--mw-code-editor-border: {codeEditorTheme.Border}; " +
            $"--mw-code-editor-comment: {codeEditorTheme.Comment}; " +
            $"--mw-code-editor-string: {codeEditorTheme.String}; " +
            $"--mw-code-editor-number: {codeEditorTheme.Number}; " +
            $"--mw-code-editor-keyword: {codeEditorTheme.Keyword}; " +
            $"--mw-code-editor-literal: {codeEditorTheme.Keyword}; " +
            $"--mw-code-editor-built-in: {codeEditorTheme.Function}; " +
            $"--mw-code-editor-constant: {codeEditorTheme.Constant}; " +
            $"--mw-code-editor-function: {codeEditorTheme.Function}; " +
            $"--mw-code-editor-property: {codeEditorTheme.Function}; " +
            $"--mw-code-editor-variable: {codeEditorTheme.Foreground};";
    }
    
    public async ValueTask DisposeAsync()
    {
        if (this.module is null)
            return;

        try
        {
            await this.module.InvokeVoidAsync("destroy", this.editorId);
            await this.module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit can already be gone while Blazor disposes the component.
        }
    }

    private sealed record CodeEditorTheme(
        string Background,
        string Foreground,
        string Border,
        string Comment,
        string String,
        string Number,
        string Keyword,
        string Function,
        string Constant);
}
