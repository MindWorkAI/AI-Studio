using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Provider.OpenAI;
using AIStudio.Tests.Tools;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.Extensions.DependencyInjection;

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
///
/// Each picture is prepared for the model on its way, by the runtime, which a test plays here.
/// A picture which cannot be prepared has to stay out of the message entirely.
/// </remarks>
[TestFixture]
public sealed class ListContentBlockExtensionsTests
{
    private string picturePath = string.Empty;
    private FakeImageRuntime runtime = null!;
    private RustService rustService = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void PrepareServices()
    {
        this.picturePath = Path.Combine(Path.GetTempPath(), $"picture-{Guid.NewGuid():N}.webp");
        File.WriteAllBytes(this.picturePath, [1, 2, 3]);

        this.runtime = new FakeImageRuntime();
        this.rustService = new RustService(this.runtime);
        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection()
            .AddSingleton(this.rustService)
            .AddSingleton<ModelImageService>()
            .AddLogging()
            .BuildServiceProvider();

        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        this.rustService.Dispose();
        this.runtime.Dispose();
        File.Delete(this.picturePath);
    }

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
    public async Task APictureWhichCannotBePreparedLeavesNoEmptyPart()
    {
        this.runtime.ErrorCode = "NOT_DECODABLE";

        var messages = await BlocksWithAPicture(this.picturePath).BuildMessagesUsingNestedImageUrlAsync(PictureReadingProvider());

        var message = messages.Single() as MultimodalMessage;
        Assert.That(message, Is.Not.Null, "The text still goes to the model.");
        Assert.That(message!.Content, Has.Count.EqualTo(1).And.All.InstanceOf<SubContentText>(), "A picture without data makes the provider refuse the whole request.");
    }

    [Test]
    public async Task TheTypeOfAPictureComesFromItsPreparation()
    {
        // The file is a WebP, which the runtime prepared as JPEG:
        var messages = await BlocksWithAPicture(this.picturePath).BuildMessagesUsingDirectImageUrlAsync(PictureReadingProvider());

        var picture = (messages.Single() as MultimodalMessage)!.Content.OfType<SubContentImageUrl>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(picture.ImageUrl, Is.EqualTo($"data:image/jpeg;base64,{FakeImageRuntime.IMAGE_BASE64}"));
            Assert.That(this.runtime.RequestedPaths, Is.EqualTo(new[] { new FileInfo(this.picturePath).FullName }));
        });
    }

    [Test]
    public async Task WithoutThatSwitchThePictureStaysOut()
    {
        var provider = new AIStudio.Settings.Provider(0, "test", "Test", LLMProviders.SELF_HOSTED, new Model("llama3.3:70b", null));

        var messages = await BlocksWithAPicture().BuildMessagesAsync(provider, _ => "user", Text, Picture);

        Assert.That(messages.Single(), Is.InstanceOf<TextMessage>(), "Nothing says this model reads pictures, so the text goes on its own.");
    }

    /// <summary>
    /// A model which reads text only, according to the rules, but which a person said reads pictures.
    /// </summary>
    private static AIStudio.Settings.Provider PictureReadingProvider() => new(0, "test", "Test", LLMProviders.SELF_HOSTED, new Model("llama3.3:70b", null))
    {
        CapabilityOverrides = new() { MultipleImageInput = true },
    };

    /// <summary>
    /// One block of text with a picture hanging on it.
    /// </summary>
    /// <param name="picturePath">Where the picture lies.</param>
    /// <returns>The blocks.</returns>
    private static List<ContentBlock> BlocksWithAPicture(string picturePath = "/tmp/picture.png") =>
    [
        new()
        {
            Role = ChatRole.USER,
            ContentType = ContentType.TEXT,
            Content = new ContentText
            {
                Text = "What is in this picture?",
                FileAttachments = [new FileAttachmentImage(Path.GetFileName(picturePath), picturePath, 1_024)],
            },
        },
    ];

    private static ISubContent Text(string text) => new SubContentText { Text = text };

    private static ISubContent Picture(PreparedModelImage image) => new SubContentText { Text = image.MimeType };
}