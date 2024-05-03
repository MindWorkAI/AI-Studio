namespace MudBlazor;

public interface IMudMarkdownClipboardService
{
	ValueTask CopyToClipboardAsync(string text);
}
