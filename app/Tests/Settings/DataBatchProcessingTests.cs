using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;

namespace AIStudio.Tests.Settings;

public class DataBatchProcessingTests
{
    /// <summary>
    /// The default patterns are a constant and cannot be built from FileTypes, so this keeps both
    /// lists of the images we hand to the model in step.
    /// </summary>
    [Test]
    public void DefaultPatternsHoldEveryImageTheModelCanRead()
    {
        var patterns = DataBatchProcessing.DEFAULT_FILE_PATTERNS.Split(';');
        var imagePatterns = FileTypes.MODEL_IMAGE.FlattenExtensions().Select(extension => $"*.{extension}");

        Assert.That(patterns, Is.SupersetOf(imagePatterns));
    }
}