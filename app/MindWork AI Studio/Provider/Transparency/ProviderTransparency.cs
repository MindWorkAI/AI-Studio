using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Transparency;

public sealed class ProviderTransparency() : ProviderTransparencyBase(LOGGER)
{
    private static readonly ILogger<ProviderTransparency> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderTransparency>();

    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        var effectiveModel = NormalizeModel(chatModel, CHAT_PREVIEW_MODEL);
        var requestBlocks = chatThread.Blocks
            .Where(block => !IsTransparencyPreviewBlock(block))
            .ToList();
        var skippedTransparencyPreviewCount = chatThread.Blocks.Count - requestBlocks.Count;
        var preparedSystemPrompt = chatThread.PrepareSystemPrompt(settingsManager);
        var systemPrompt = new TextMessage
        {
            Role = "system",
            Content = preparedSystemPrompt,
        };

        var apiParameters = this.ParseAdditionalApiParameters();
        var messages = await requestBlocks.BuildMessagesUsingDirectImageUrlAsync(this.Provider, effectiveModel);
        var requestBody = JsonSerializer.Serialize(new ChatCompletionAPIRequest
        {
            Model = effectiveModel.Id,
            Messages = [systemPrompt, ..messages],
            Stream = true,
            AdditionalApiParameters = apiParameters,
        }, JSON_SERIALIZER_OPTIONS);
        var readableBreakdown = this.BuildReadableBreakdown(chatThread, preparedSystemPrompt, requestBlocks, skippedTransparencyPreviewCount, settingsManager);

        yield return new ContentStreamChunk(
            this.BuildJsonRequestPreview(
                "chat/completions",
                effectiveModel,
                requestBody,
                readableBreakdown,
                ("Message count", (messages.Count + 1).ToString()),
                ("Stream", bool.TrueString),
                ("Additional API parameters", apiParameters.Count.ToString())),
            []);
    }

    private string BuildReadableBreakdown(ChatThread chatThread, string preparedSystemPrompt, IReadOnlyList<ContentBlock> requestBlocks, int skippedTransparencyPreviewCount, SettingsManager settingsManager)
    {
        var builder = new StringBuilder();
        AppendSystemPromptBreakdown(builder, chatThread, preparedSystemPrompt, settingsManager);
        AppendConversationBreakdown(builder, chatThread, requestBlocks, skippedTransparencyPreviewCount, settingsManager);
        return builder.ToString().TrimEnd();
    }

    private static void AppendSystemPromptBreakdown(StringBuilder builder, ChatThread chatThread, string preparedSystemPrompt, SettingsManager settingsManager)
    {
        var baseSystemPrompt = chatThread.SystemPrompt;
        var selectedTemplate = ResolveSelectedChatTemplate(chatThread, settingsManager);
        var selectedProfile = ResolveSelectedProfile(chatThread, settingsManager);

        builder.AppendLine("Final system prompt sent to the provider:");
        builder.AppendLine("```text");
        builder.AppendLine(preparedSystemPrompt);
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("System prompt sources:");

        if (!string.IsNullOrWhiteSpace(baseSystemPrompt))
        {
            builder.AppendLine("- Base chat system prompt:");
            builder.AppendLine("```text");
            builder.AppendLine(baseSystemPrompt);
            builder.AppendLine("```");
        }

        if (selectedTemplate is not null)
        {
            builder.AppendLine($"- Chat template system prompt from `{selectedTemplate.GetSafeName()}`:");
            builder.AppendLine("```text");
            builder.AppendLine(selectedTemplate.ToSystemPrompt());
            builder.AppendLine("```");

            if (selectedTemplate.ExampleConversation.Count > 0)
                builder.AppendLine($"- Chat template example conversation adds `{selectedTemplate.ExampleConversation.Count}` hidden message(s) to the request history.");
        }

        if (!string.IsNullOrWhiteSpace(chatThread.AugmentedData))
        {
            builder.AppendLine("- Augmented context added by AI Studio:");
            builder.AppendLine("```text");
            builder.AppendLine(chatThread.AugmentedData);
            builder.AppendLine("```");
        }

        if (selectedProfile is not null && selectedTemplate?.AllowProfileUsage != false)
        {
            if (!string.IsNullOrWhiteSpace(selectedProfile.NeedToKnow))
            {
                builder.AppendLine($"- Profile `{selectedProfile.GetSafeName()}`: What should you know about the user?");
                builder.AppendLine("```text");
                builder.AppendLine(selectedProfile.NeedToKnow);
                builder.AppendLine("```");
            }

            if (!string.IsNullOrWhiteSpace(selectedProfile.Actions))
            {
                builder.AppendLine($"- Profile `{selectedProfile.GetSafeName()}`: The user wants you to consider the following things.");
                builder.AppendLine("```text");
                builder.AppendLine(selectedProfile.Actions);
                builder.AppendLine("```");
            }
        }

        if (selectedTemplate is not null && !selectedTemplate.AllowProfileUsage)
            builder.AppendLine($"- Profile instructions are disabled by chat template `{selectedTemplate.GetSafeName()}`.");

        if (chatThread.IncludeDateTime)
            builder.AppendLine("- AI Studio prepends the current UTC and local date/time before the system prompt.");
    }

    private static void AppendConversationBreakdown(StringBuilder builder, ChatThread chatThread, IReadOnlyList<ContentBlock> requestBlocks, int skippedTransparencyPreviewCount, SettingsManager settingsManager)
    {
        var selectedTemplate = ResolveSelectedChatTemplate(chatThread, settingsManager);
        var orderedBlocks = requestBlocks
            .Where(block => block.ContentType is ContentType.TEXT && block.Content is ContentText text && !string.IsNullOrWhiteSpace(text.Text))
            .OrderBy(block => block.Time)
            .ToList();

        builder.AppendLine();
        builder.AppendLine("Conversation history sources:");

        if (skippedTransparencyPreviewCount > 0)
            builder.AppendLine($"- Previous transparency preview responses are excluded from the generated request: `{skippedTransparencyPreviewCount}`.");

        if (orderedBlocks.Count == 0)
        {
            builder.AppendLine("- No prior chat messages are part of this request.");
            return;
        }

        var templateMessageCount = CountTemplateExampleMessages(orderedBlocks, selectedTemplate);
        if (templateMessageCount > 0)
        {
            builder.AppendLine($"- Chat template example conversation from `{selectedTemplate!.GetSafeName()}`:");
            builder.AppendLine("```text");
            foreach (var block in orderedBlocks.Take(templateMessageCount))
                builder.AppendLine(SummarizeBlock(block));
            builder.AppendLine("```");
        }

        var remainingBlocks = orderedBlocks.Skip(templateMessageCount).ToList();
        var visibleBlocks = remainingBlocks.Where(block => !block.HideFromUser).ToList();
        var hiddenBlocks = remainingBlocks.Where(block => block.HideFromUser).ToList();

        if (visibleBlocks.Count > 0)
        {
            builder.AppendLine("- Visible chat history and current user input:");
            builder.AppendLine("```text");
            foreach (var block in visibleBlocks)
                builder.AppendLine(SummarizeBlock(block));
            builder.AppendLine("```");
        }

        if (hiddenBlocks.Count > 0)
        {
            builder.AppendLine("- Hidden messages included in the request:");
            builder.AppendLine("```text");
            foreach (var block in hiddenBlocks)
                builder.AppendLine(SummarizeBlock(block));
            builder.AppendLine("```");
        }
    }

    private static ChatTemplate? ResolveSelectedChatTemplate(ChatThread chatThread, SettingsManager settingsManager)
    {
        if (string.IsNullOrWhiteSpace(chatThread.SelectedChatTemplate))
            return null;

        if (!Guid.TryParse(chatThread.SelectedChatTemplate, out var templateId) || templateId == Guid.Empty || chatThread.SelectedChatTemplate == ChatTemplate.NO_CHAT_TEMPLATE.Id)
            return null;

        return settingsManager.ConfigurationData.ChatTemplates.FirstOrDefault(template => template.Id == chatThread.SelectedChatTemplate);
    }

    private static Profile? ResolveSelectedProfile(ChatThread chatThread, SettingsManager settingsManager)
    {
        if (string.IsNullOrWhiteSpace(chatThread.SelectedProfile))
            return null;

        if (!Guid.TryParse(chatThread.SelectedProfile, out var profileId) || profileId == Guid.Empty || chatThread.SelectedProfile == Profile.NO_PROFILE.Id)
            return null;

        return settingsManager.ConfigurationData.Profiles.FirstOrDefault(profile => profile.Id == chatThread.SelectedProfile);
    }

    private static int CountTemplateExampleMessages(IReadOnlyList<ContentBlock> orderedBlocks, ChatTemplate? selectedTemplate)
    {
        if (selectedTemplate is null || selectedTemplate.ExampleConversation.Count == 0)
            return 0;

        var matchingCount = 0;
        foreach (var pair in orderedBlocks.Zip(selectedTemplate.ExampleConversation))
        {
            if (pair.First.Role != pair.Second.Role)
                break;

            if (pair.First.Content is not ContentText firstText || pair.Second.Content is not ContentText secondText)
                break;

            if (!string.Equals(firstText.Text.Trim(), secondText.Text.Trim(), StringComparison.Ordinal))
                break;

            matchingCount++;
        }

        return matchingCount;
    }

    private static string SummarizeBlock(ContentBlock block)
    {
        if (block.Content is not ContentText contentText)
            return $"{block.Role.ToChatTemplateName()}: [unsupported content]";

        var attachmentSuffix = contentText.FileAttachments.Count == 0
            ? string.Empty
            : $" [attachments: {contentText.FileAttachments.Count}]";

        var visibilitySuffix = block.HideFromUser ? " [hidden]" : string.Empty;
        return $"{block.Role.ToChatTemplateName()}: {contentText.Text.Trim()}{attachmentSuffix}{visibilitySuffix}";
    }

    private static bool IsTransparencyPreviewBlock(ContentBlock block)
    {
        if (block.Role is not ChatRole.AI || block.Content is not ContentText contentText)
            return false;

        return contentText.Text.StartsWith(PREVIEW_NOTICE, StringComparison.Ordinal);
    }
}
