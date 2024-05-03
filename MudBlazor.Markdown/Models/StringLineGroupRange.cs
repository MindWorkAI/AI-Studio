namespace MudBlazor;

internal readonly ref struct StringLineGroupRange
{
	public StringLineGroupRange(StringLineGroupIndex start, StringLineGroupIndex end)
	{
		Start = start;
		End = end;
	}

	public StringLineGroupIndex Start { get; }

	public StringLineGroupIndex End { get; }
}
