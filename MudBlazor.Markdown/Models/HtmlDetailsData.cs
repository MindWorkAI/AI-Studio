namespace MudBlazor;

internal readonly ref struct HtmlDetailsData
{
	public HtmlDetailsData(string header, string content)
	{
		Header = header;
		Content = content;
	}

	public string Header { get; }

	public string Content { get; }
}
