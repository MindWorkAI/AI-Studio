using System.Text;

namespace MudBlazor;

internal static class StringLineGroupEx
{
	public static bool StartsAndEndsWith(this StringLineGroup @this, in string startsWith, in string endsWith, out StringLineGroupRange range)
	{
		range = new StringLineGroupRange();

		if (@this.Count == 0)
			return false;

		const int firstLineIndex = 0;
		var lastLineIndex = @this.Count - 1;

		// Starts with
		var firstLine = @this.Lines[firstLineIndex];
		if (firstLine.Slice.Length < startsWith.Length)
			return false;

		var startIndex = 0;
		for (;startIndex < startsWith.Length; startIndex++)
			if (firstLine.Slice[firstLine.Position + startIndex] != startsWith[startIndex])
				return false;

		// Ends with
		var lastLine = @this.Lines[lastLineIndex];
		if (lastLine.Slice.Length < endsWith.Length)
			return false;

		var endIndex = lastLine.Slice.Length - 1;
		for (var i = endsWith.Length - 1; i >= 0; i--, endIndex--)
			if (endsWith[i] != lastLine.Slice[lastLine.Position + endIndex])
				return false;

		range = new StringLineGroupRange(
			new StringLineGroupIndex(firstLineIndex, startIndex),
			new StringLineGroupIndex(lastLineIndex, endIndex));

		return true;
	}

	public static StringLineGroupIndex TryGetContent(this StringLineGroup @this, in string startsWith, in string endsWith, in StringLineGroupIndex startIndex, out string content)
	{
		var endIndex = new StringLineGroupIndex(-1, -1);

		var isFound = false;
		var stringBuilder = new StringBuilder();

		for (var i = startIndex.Line; i < @this.Count; i++)
		{
			for (var j = i == startIndex.Line ? startIndex.Index : 0; j < @this.Lines[i].Slice.Length; j++)
			{
				if (!isFound)
				{
					var start = @this.Lines[i].IndexOf(startsWith);
					if (start == -1)
						continue;

					isFound = true;
					j = @this.Lines[i].IndexOf(">", start);
				}
				else
				{
					var end = @this.Lines[i].IndexOf(endsWith);
					if (end == -1)
					{
						var strValue = @this.Lines[i].ToString().Trim();
						stringBuilder.Append(strValue);
						break;
					}
					else
					{
						var strValue = @this.Lines[i].Slice.AsSpan()[j..end].ToString().Trim();
						stringBuilder.Append(strValue);

						endIndex = new StringLineGroupIndex(i, end + endsWith.Length);
						goto Return;
					}
				}
			}
		}

		Return:
		content = stringBuilder.ToString();
		return endIndex;
	}

	public static string GetContent(this StringLineGroup @this, in StringLineGroupIndex startIndex, in StringLineGroupIndex endIndex)
	{
		var stringBuilder = new StringBuilder();

		for (var i = startIndex.Line; i <= endIndex.Line; i++)
		{
			if (i == startIndex.Line)
			{
				if (i == endIndex.Line)
				{
					var strValue = @this.Lines[i].Slice.AsSpan()[startIndex.Index..endIndex.Index].ToString().Trim();
					stringBuilder.Append(strValue);

					break;
				}
				else
				{
					var strValue = @this.Lines[i].Slice.AsSpan()[startIndex.Index..].ToString().Trim();
					stringBuilder.Append(strValue);
				}
			}
			else if (i == endIndex.Line)
			{
				if (endIndex.Index == -1)
					break;

				var strValue = @this.Lines[i].Slice.AsSpan()[..endIndex.Index].ToString().Trim();
				stringBuilder.Append(strValue);
			}
			else
			{
				if (stringBuilder.Length != 0)
					stringBuilder.AppendLine();

				var strValue = @this.Lines[i].ToString().Trim();
				stringBuilder.Append(strValue);
			}
		}

		return stringBuilder.ToString();
	}
}
