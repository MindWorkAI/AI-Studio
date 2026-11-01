using AIStudio.Provider;
using AIStudio.Provider.SelfHosted;

namespace AIStudio.Tests.Provider.SelfHosted;

/// <summary>
/// Checks that what each self-hosted engine can do is stated once, and that the statements fit together.
/// </summary>
/// <remarks>
/// Which engines can list their models used to be written down twice, once for the provider and once
/// for the dialogs, and the loading of the chat, embedding, and transcription lists repeated the
/// engines a third time. A new engine had to be added to each of these lists, and one it was
/// forgotten in left a list empty or a reload button disabled without anything failing. Now the
/// host extensions state it, and these cases check that every other answer follows from them.
/// </remarks>
[TestFixture]
public sealed class HostExtensionsTests
{
    private static readonly Host[] ALL_HOSTS = Enum.GetValues<Host>();

    [TestCaseSource(nameof(ALL_HOSTS))]
    public void TheDialogsOfferToLoadModelsExactlyWhereTheEngineCanListThem(Host host)
    {
        Assert.That(LLMProviders.SELF_HOSTED.CanLoadModels(host, null), Is.EqualTo(host.IsModelListSupported()));
    }

    [Test]
    public void EveryEngineForChatsCanListItsModels()
    {
        //
        // There is no fixed chat model to offer instead, so such an engine would offer an empty list:
        //
        var unlisted = ALL_HOSTS.Where(host => host is not Host.NONE && host.IsChatSupported() && !host.IsModelListSupported());
        Assert.That(unlisted, Is.Empty);
    }

    [Test]
    public void EveryEngineForEmbeddingsCanListItsModels()
    {
        //
        // There is no fixed embedding model to offer instead, so such an engine would offer an empty list:
        //
        var unlisted = ALL_HOSTS.Where(host => host.IsEmbeddingSupported() && !host.IsModelListSupported());
        Assert.That(unlisted, Is.Empty);
    }

    [Test]
    public void OnlyWhisperCppTranscribesWithoutListingItsModels()
    {
        //
        // The provider offers the one model of whisper.cpp under a fixed name. Any other engine
        // which transcribes but cannot list its models would offer an empty list:
        //
        var unlisted = ALL_HOSTS.Where(host => host.IsTranscriptionSupported() && !host.IsModelListSupported());
        Assert.That(unlisted, Is.EqualTo(new[] { Host.WHISPER_CPP }));
    }

    [Test]
    public void TheHostNoneLoadsNoModels()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Host.NONE.IsModelListSupported(), Is.False);
            Assert.That(Host.NONE.IsEmbeddingSupported(), Is.False);
            Assert.That(Host.NONE.IsTranscriptionSupported(), Is.False);
            Assert.That(LLMProviders.SELF_HOSTED.CanLoadModels(Host.NONE, "a key"), Is.False);
        }
    }
}