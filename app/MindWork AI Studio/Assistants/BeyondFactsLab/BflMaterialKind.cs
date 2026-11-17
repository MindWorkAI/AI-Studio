namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What a material of the BeyondFacts Lab came from, which decides how the lab keeps it and how it reaches a model.
/// </summary>
public enum BflMaterialKind
{
    /// <summary>
    /// A document, e.g., a paper or slides: the lab keeps its text. Also every material stored before the lab knew
    /// other kinds.
    /// </summary>
    DOCUMENT = 0,

    /// <summary>
    /// An audio or video recording: the lab keeps the text of its transcript.
    /// </summary>
    RECORDING,

    /// <summary>
    /// An image, e.g., a poster: the lab keeps a copy prepared for models, which goes along with every message to a
    /// model which can see images.
    /// </summary>
    IMAGE,
}