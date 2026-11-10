namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whether an image of the material reaches the model of a conversation.
/// </summary>
public enum BflImageDelivery
{
    /// <summary>
    /// The material is no image.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// The image goes along with the first message of the conversation, with every request.
    /// </summary>
    SENT,

    /// <summary>
    /// The model cannot see images; it learns only the name of the image.
    /// </summary>
    MODEL_SEES_NO_IMAGES,

    /// <summary>
    /// The model takes fewer images at once than the material holds, and the earlier ones took the room; it learns
    /// only the name of the image.
    /// </summary>
    OVER_THE_LIMIT,
}