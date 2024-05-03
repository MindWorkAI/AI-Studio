namespace MudBlazor;

internal sealed class MudMarkdownThemeService : IMudMarkdownThemeService
{
	public event EventHandler<CodeBlockTheme>? CodeBlockThemeChanged;

	public void SetCodeBlockTheme(CodeBlockTheme theme) =>
		CodeBlockThemeChanged?.Invoke(this, theme);
}