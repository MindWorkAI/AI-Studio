using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants;

public abstract partial class AssistantBase<TSettings> where TSettings : IComponent
{
    /// <summary>
    /// Sends one request in a thread of its own, which the user never sees, and waits for the whole answer.
    /// </summary>
    /// <remarks>
    /// For assistants which split their work into many requests, e.g., the segments of a document. The thread
    /// gets no tools, since the request carries all the model needs, and no profile. It runs with the selected
    /// provider and with the restrictions of whatever was sent to this assistant, see AssignRuntimeIdentity.
    /// </remarks>
    /// <param name="systemPrompt">The system prompt of the request.</param>
    /// <param name="request">The request.</param>
    /// <param name="token">Cancels the request.</param>
    /// <returns>The answer without the thoughts of the model; empty when the provider delivered nothing.</returns>
    protected async Task<string> AskInPrivateThreadAsync(string systemPrompt, string request, CancellationToken token)
    {
        var thread = new ChatThread
        {
            IncludeDateTime = false,
            SelectedProvider = this.ProviderSettings.Id,
            SelectedProfile = Profile.NO_PROFILE.Id,
            SelectedToolIds = [],
            SystemPrompt = systemPrompt,
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = this.Title,
            Blocks = [],
            RuntimeSelectedToolIds = [],
            RuntimeToolsAreAssistantManaged = true,
        };

        this.AssignRuntimeIdentity(thread);
        var userPrompt = new ContentText { Text = request };
        thread.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = userPrompt,
            HideFromUser = true,
        });

        var answer = new ContentText();
        thread.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = answer,
            HideFromUser = true,
        });

        await answer.CreateFromProviderAsync(this.ProviderSettings.CreateProvider(), this.ProviderSettings.Model, userPrompt, thread, token);
        token.ThrowIfCancellationRequested();
        return answer.Text;
    }
}