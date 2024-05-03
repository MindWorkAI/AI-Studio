namespace MudBlazor;

internal readonly ref struct StringLineGroupIndex
{
	public StringLineGroupIndex(int line, int index)
	{
		Line = line;
		Index = index;
	}

	public int Line { get; }

	public int Index { get; }
}
