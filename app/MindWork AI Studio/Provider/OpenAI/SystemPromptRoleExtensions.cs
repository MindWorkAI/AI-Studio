using AIStudio.Models;

namespace AIStudio.Provider.OpenAI;

public static class SystemPromptRoleExtensions
{
    /// <param name="role">The role the model profile states.</param>
    extension(SystemPromptRole role)
    {
        /// <summary>
        /// Spells a system prompt role the way OpenAI's APIs expect it.
        /// </summary>
        /// <remarks>
        /// A role no rule states becomes "developer". OpenAI's current documentation names no other, a
        /// model the rules do not know is almost always one released after they were written, and the
        /// older generations which still want "system" say so in their families.
        /// </remarks>
        /// <returns>The role, as OpenAI names it.</returns>
        public string ToOpenAIRole() => role switch
        {
            SystemPromptRole.SYSTEM => "system",
            SystemPromptRole.DEVELOPER => "developer",
            SystemPromptRole.USER => "user",

            _ => "developer",
        };
    }
}