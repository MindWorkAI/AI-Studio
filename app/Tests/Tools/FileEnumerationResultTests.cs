using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

public sealed class FileEnumerationResultTests
{
    [Test]
    public void PatternExclusionsAreSummarizedByFileTypeWithoutKeepingEveryPath()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "test-data");
        var result = new FileEnumerationResult();

        result.AddFilePatternExclusion(Path.Combine(directoryPath, "one.PDF"));
        result.AddFilePatternExclusion(Path.Combine(directoryPath, "two.pdf"));
        result.AddFilePatternExclusion(Path.Combine(directoryPath, "README"));
        result.AddFilePatternExclusion(Path.Combine(directoryPath, "Dockerfile"));
        result.AddFilePatternExclusion(Path.Combine(directoryPath, "nested", "dockerfile"));
        result.AddFilePatternExclusion(Path.Combine(directoryPath, "Caddyfile"));

        Assert.Multiple(() =>
        {
            Assert.That(result.FilePatternExclusionCount, Is.EqualTo(6));
            Assert.That(result.FilePatternExclusions, Is.EquivalentTo(new[]
            {
                new DataSourceEmbeddingFilePatternGroup(string.Empty, 1),
                new DataSourceEmbeddingFilePatternGroup("Caddyfile", 1),
                new DataSourceEmbeddingFilePatternGroup("Dockerfile", 2),
                new DataSourceEmbeddingFilePatternGroup(".pdf", 2),
            }));
        });
    }
}
