using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MudBlazor;

internal static class HeadingBlockEx
{
	private const char JoinChar = '-';
	private static readonly string[] EscapeChars = { "+", ":", "&" };

	public static string? BuildIdString(this HeadingBlock @this)
	{
		if (@this.Inline == null)
			return null;

		var slices = @this.Inline
			.Select(static x => x.GetStringContent())
			.Where(static x => x.Length > 0);

		return string.Join(JoinChar, slices);
	}

	private static string GetStringContent(this Inline @this)
	{
		var slice = @this switch
		{
			LiteralInline x => x.Content,
			_ => StringSlice.Empty
		};

		return PrepareStringContent(slice.ToString());
	}

	private static string PrepareStringContent(this string @this)
	{
		var words = @this.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var str = string.Join(JoinChar, words).ToLower();

		for (var i = 0; i < EscapeChars.Length; i++)
			str = str.Replace(EscapeChars[i], string.Empty);

		return str;
	}
}