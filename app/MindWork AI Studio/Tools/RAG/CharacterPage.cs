namespace AIStudio.Tools.RAG;

/// <summary>
/// One page of results, cut by their length, see CharacterPaging.
/// </summary>
/// <param name="Start">The index of the first result on the page.</param>
/// <param name="Count">How many results the page holds, at least one.</param>
public readonly record struct CharacterPage(int Start, int Count)
{
    /// <summary>
    /// The index right after the last result on the page, i.e., where the next page starts.
    /// </summary>
    public int End => this.Start + this.Count;
}