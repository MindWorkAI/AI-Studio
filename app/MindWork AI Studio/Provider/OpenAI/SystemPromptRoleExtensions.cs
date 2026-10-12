using AIStudio.Models;

namespace AIStudio.Provider.OpenAI;

public static class SystemPromptRoleExtensions
{
    /// <summary>
    /// Spells a system prompt role the way OpenAI's APIs expect it.
    /// </summary>
    /// <remarks>
    /// A role no rule states becomes "developer". OpenAI's current documentation names no other, a
    /// model the rules do not know is almost always one released after they were written, and the
    /// older generations which still want "system" say so in their families.
    /// </remarks>
    /// <param name="role">The role the model profile states.</param>
    /// <returns>The role, as OpenAI names it.</returns>
    public static string ToOpenAIRole(this SystemPromptRole role) => role switch
    {
        SystemPromptRole.SYSTEM => "system",
        SystemPromptRole.DEVELOPER => "developer",
        SystemPromptRole.USER => "user",

        _ => "developer",
    };
}