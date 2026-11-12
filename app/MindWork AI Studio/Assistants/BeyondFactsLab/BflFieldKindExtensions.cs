namespace AIStudio.Assistants.BeyondFactsLab;

public static class BflFieldKindExtensions
{
    /// <param name="kind">The kind of field.</param>
    extension(BflFieldKind kind)
    {
        /// <summary>
        /// Gets the longest value a field of this kind takes, in characters.
        /// </summary>
        /// <remarks>
        /// The values go into the prompts of the chats, so each kind gets a limit that leaves room for the
        /// rest of a preparation.
        /// </remarks>
        public int MaxLength => kind switch
        {
            BflFieldKind.SINGLE_LINE => 300,
            BflFieldKind.MULTI_LINE => 4_000,
            BflFieldKind.LINK => 2_000,
            BflFieldKind.CHOICE => 100,
            BflFieldKind.MULTI_CHOICE => 1_000,

            _ => 0,
        };

        /// <summary>
        /// Gets whether a field of this kind offers options to choose from.
        /// </summary>
        public bool HasOptions => kind is BflFieldKind.CHOICE or BflFieldKind.MULTI_CHOICE;
    }
}