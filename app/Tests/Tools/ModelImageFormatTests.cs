using AIStudio.Chat;
using AIStudio.Tools.Rust;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which image formats may be attached for an AI model.
/// </summary>
/// <remarks>
/// The runtime decides which formats it can prepare for a model, see prepare_image_for_model in
/// runtime/src/image.rs. An image the app lets through but the runtime cannot prepare would only
/// fail once the message is sent; one the app refuses although the runtime could prepare it would
/// be refused for nothing.
/// </remarks>
[TestFixture]
public sealed class ModelImageFormatTests
{
    /// <summary>
    /// The extensions model_input_format in runtime/src/image.rs accepts.
    /// </summary>
    private static readonly string[] RUNTIME_MODEL_FORMATS = ["jpg", "jpeg", "png", "webp", "gif", "bmp", "tif", "tiff"];

    [Test]
    public void EveryFormatTheRuntimePreparesMayBeAttached()
    {
        Assert.That(FileTypes.MODEL_IMAGE.FlattenExtensions(), Is.EquivalentTo(RUNTIME_MODEL_FORMATS));
    }

    [Test]
    public void OnlyFormatsWithoutADecoderAreRefused()
    {
        var refused = FileTypes.IMAGE.FlattenExtensions().Except(FileTypes.MODEL_IMAGE.FlattenExtensions());

        Assert.That(refused, Is.EquivalentTo(new[] { "svg", "heic", "avif" }));
    }

    [Test]
    public void ATifFileIsAnImage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileAttachment.FromPath("/tmp/scan.tif").IsImage, Is.True);
            Assert.That(FileTypes.IsAllowedPath("/tmp/scan.tif", FileTypes.MODEL_IMAGE), Is.True);
        });
    }
}