using AIStudio.Tools;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings;

/// <summary>
/// A glossary: terms with a fixed translation or an explanation, which AI Studio hands to the model.
/// </summary>
/// <remarks>
/// The settings hold only what describes the glossary. Its entries can run into the thousands, so they
/// live in a file of their own, which the GlossaryStore derives from the ID. A spreadsheet or CSV file
/// serves only to import entries once; afterward, AI Studio keeps them itself.
/// </remarks>
/// <param name="Num">The number of the glossary.</param>
/// <param name="Id">The unique ID of the glossary.</param>
/// <param name="Name">The name the user sees.</param>
/// <param name="Type">What the values of the glossary mean.</param>
/// <param name="Description">What the glossary is about and when it applies, for the user and the model.</param>
/// <param name="SourceLanguage">The language of the terms; only a translation glossary needs it.</param>
/// <param name="CustomSourceLanguage">The language of the terms when the source language is OTHER.</param>
/// <param name="TargetLanguage">The language of the values; only a translation glossary needs it.</param>
/// <param name="CustomTargetLanguage">The language of the values when the target language is OTHER.</param>
/// <param name="PreselectInTranslation">Whether the translation assistant uses the glossary until the user deselects it, provided it suits the target language.</param>
/// <param name="IsEnterpriseConfiguration">Whether a configuration plugin provides the glossary.</param>
/// <param name="EnterpriseConfigurationPluginId">The configuration plugin which provides the glossary.</param>
public sealed record Glossary(
    uint Num,
    string Id,
    string Name,
    GlossaryType Type,
    string Description,
    CommonLanguages SourceLanguage,
    string CustomSourceLanguage,
    CommonLanguages TargetLanguage,
    string CustomTargetLanguage,
    bool PreselectInTranslation,
    bool IsEnterpriseConfiguration = false,
    Guid EnterpriseConfigurationPluginId = default) : ConfigurationBaseObject
{
    //
    // A new glossary is meant to be used: whoever adds a glossary into English expects the next
    // translation into English to follow it.
    //
    public Glossary() : this(0, Guid.Empty.ToString(), string.Empty, GlossaryType.TRANSLATION, string.Empty, CommonLanguages.AS_IS, string.Empty, CommonLanguages.AS_IS, string.Empty, PreselectInTranslation: true)
    {
    }

    public static readonly Glossary NO_GLOSSARY = new()
    {
        Num = uint.MaxValue,
    };

    #region Overrides of ValueType

    public override string ToString() => this.Name;

    #endregion
}