using Markdig.Syntax;

namespace MudBlazor;

internal static class HtmlBlockEx
{
	public static bool TryGetDetails(this HtmlBlock @this, out HtmlDetailsData htmlDetailsData)
	{
		htmlDetailsData = new HtmlDetailsData();

		// Closing `>` for <details> is missing because there might be attributes for this tag
		if (!@this.Lines.StartsAndEndsWith("<details", "</details>", out var range))
			return false;

		// Closing `>` for <summary> is missing because there might be attributes for this tag
		var summaryEndIndex = @this.Lines.TryGetContent("<summary", "</summary>", range.Start, out var headerContent);
		if (summaryEndIndex.Line == -1)
			return false;

		var dataContent = @this.Lines.GetContent(summaryEndIndex, range.End);

		htmlDetailsData = new HtmlDetailsData(headerContent, dataContent);
		return true;
	}
}
