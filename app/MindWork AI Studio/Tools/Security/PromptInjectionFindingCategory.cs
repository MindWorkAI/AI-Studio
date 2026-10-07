using System.Text.Json.Serialization;

namespace AIStudio.Tools.Security;

[JsonConverter(typeof(PromptInjectionFindingCategoryJsonConverter))]
public enum PromptInjectionFindingCategory
{
    UNKNOWN = 0,
    OVERRIDE,
    ROLE_OVERRIDE,
    EXFILTRATION,
    JAILBREAK,
    AGENT_MANIPULATION,
    DELIMITER_EVASION,
    MARKUP_EVASION,
    ENCODING_EVASION,
    PERSISTENCE,
    EVASION,
}