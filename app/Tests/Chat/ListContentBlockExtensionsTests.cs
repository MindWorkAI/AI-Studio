using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that writing a message asks the same question as attaching the picture did.
/// </summary>
/// <remarks>
/// These were two questions until now. Attaching a file asked the configured provider, so a person's
/// expert settings counted; building the message asked the automatic answer alone, so they did not.
/// Somebody who switched image input on for their own installation watched the picture attach and
/// then watched it disappear on the way to the model -- every chat round and every tool round, with
/// nothing anywhere saying why.
/// </remarks>
[TestFixture]
public sealed class ListContentBlockExtensionsTests
{
    [Test]
    public async Task ImageInputSwitchedOnByHandReachesTheMessageAsWell()
    {
        var provider = new AIStudio.Settings.Provider(0, "test", "Test", LLMProviders.SELF_HOSTED, new Model("llama3.3:70b", null))
        {
            // The rules say this model reads text only, which is what makes it the right model here:
            CapabilityOverrides = new() { MultipleImageInput = true },
        };

        var messages = await BlocksWithAPicture().BuildMessagesAsync(provider, _ => "user", Text, Picture);

        Assert.That(messages.Single(), Is.InstanceOf<MultimodalMessage>(), "The picture is part of the message because the person said this model can read one.");
    }

    [Test]
    public async Task WithoutThatSwitchThePictureStaysOut()
    {
        var provider = new AIStudio.Settings.Provider(0, "test", "Test", LLMProviders.SELF_HOSTED, new Model("llama3.3:70b", null));

        var messages = await BlocksWithAPicture().BuildMessagesAsync(provider, _ => "user", Text, Picture);

        Assert.That(messages.Single(), Is.InstanceOf<TextMessage>(), "Nothing says this model reads pictures, so the text goes on its own.");
    }

    /// <summary>
    /// One block of text with a picture hanging on it.
    /// </summary>
    /// <returns>The blocks.</returns>
    private static List<ContentBlock> BlocksWithAPicture() =>
    [
        new()
        {
            Role = ChatRole.USER,
            ContentType = ContentType.TEXT,
            Content = new ContentText
            {
                Text = "What is in this picture?",
                FileAttachments = [new FileAttachmentImage("picture.png", "/tmp/picture.png", 1_024)],
            },
        },
    ];

    private static ISubContent Text(string text) => new SubContentText { Text = text };

    private static Task<ISubContent> Picture(FileAttachmentImage image) => Task.FromResult<ISubContent>(new SubContentText { Text = image.FileName });
}