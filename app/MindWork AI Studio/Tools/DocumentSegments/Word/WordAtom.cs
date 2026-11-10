namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// The elements one tag stands for, which go back into the paragraph together and in their order.
/// </summary>
/// <param name="Pieces">The elements.</param>
/// <param name="Order">Where the elements stood in the paragraph, so that those the model dropped follow its words in their order.</param>
internal sealed record WordAtom(IReadOnlyList<WordPiece> Pieces, int Order);