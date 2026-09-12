namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// Says where a spelling in the corpus comes from.
/// </summary>
/// <remarks>
/// A corpus is only worth as much as the names in it. Anybody can invent a model ID which makes a
/// rule look right, so every entry has to say who writes the name that way. The values below are
/// ordered by how easy the claim is to check: the first three point at something in this repository,
/// the last one does not and is the reason the fallback needs testing at all.
/// </remarks>
public enum CorpusOrigin
{
    /// <summary>
    /// A rule in the current capability code names this spelling literally.
    /// </summary>
    NAMED_BY_A_RULE,

    /// <summary>
    /// The app carries this model in a built-in list, such as the one Alibaba Cloud models are
    /// picked from when the provider serves no catalog.
    /// </summary>
    BUILT_INTO_THE_APP,

    /// <summary>
    /// A comment in the current capability code quotes this spelling as an example of how some
    /// host writes model names: an Ollama tag, a Fireworks path, a hub prefix, a Blablador
    /// sentence.
    /// </summary>
    QUOTED_AS_A_NAME_SHAPE,

    /// <summary>
    /// The manual verification list of the rebuild plan asks for this model.
    /// </summary>
    ON_THE_MANUAL_TEST_LIST,

    /// <summary>
    /// A name the provider serves which no rule literal mentions. These are the entries which say
    /// what happens to everything the rules were not written for.
    /// </summary>
    NAMED_BY_NO_RULE,
}