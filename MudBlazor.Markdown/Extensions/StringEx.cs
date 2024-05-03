namespace MudBlazor;

internal static class StringEx
{
	public static bool IsExternalUri(this string? @this, string? baseUri)
	{
		if (string.IsNullOrEmpty(@this) || string.IsNullOrEmpty(baseUri))
			return false;

		try
		{
			var uri = new Uri(@this, UriKind.RelativeOrAbsolute);
			return uri.IsAbsoluteUri && !@this.StartsWith(baseUri);
		}
		catch
		{
			return false;
		}
	}

	public static int ParseOrDefault(this string? @this)
	{
		if (!int.TryParse(@this, out var intValue))
			intValue = 0;

		return intValue;
	}
}
