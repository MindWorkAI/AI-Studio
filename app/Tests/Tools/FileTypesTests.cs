using System.Reflection;
using System.Text;

using AIStudio.Tools.Rust;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools;

public class FileTypesTests
{
    [TestCase("Dockerfile")]
    [TestCase("dockerfile")]
    [TestCase("Dockerfile.development")]
    [TestCase("Containerfile.arm64")]
    [TestCase("Jenkinsfile.release")]
    [TestCase("Makefile")]
    [TestCase("GNUmakefile")]
    [TestCase("Procfile")]
    [TestCase("Vagrantfile")]
    [TestCase("Caddyfile.production")]
    [TestCase("BUILD")]
    [TestCase("WORKSPACE")]
    public void DocumentRecognizesSourceLikeFileNameExceptions(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-studio-source-name-{Guid.NewGuid():N}", fileName);

        Assert.That(FileTypes.IsAllowedPath(path, FileTypes.DOCUMENT), Is.True);
    }

    [TestCase("build")]
    [TestCase("dockerfile")]
    [TestCase("makefile")]
    [TestCase("workspace")]
    public void FileNamesAreNoExtensions(string extension)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileTypes.IsAllowedExtension(extension, FileTypes.DOCUMENT), Is.False);
            Assert.That(FileTypes.IsAllowedExtension(extension, FileTypes.SOURCE_CODE), Is.False);
        });
    }

    [Test]
    public void FlattenedExtensionsHoldNoFileNames()
    {
        var fileNames = FileTypes.SOURCE_LIKE_FILE_NAMES.FileNames.Concat(FileTypes.SOURCE_LIKE_FILE_NAME_PREFIXES.FileNamePrefixes);

        Assert.That(FileTypes.OnlyAllowTypes(FileTypes.DOCUMENT).Intersect(fileNames, StringComparer.OrdinalIgnoreCase), Is.Empty);
    }

    [Test]
    public void EveryExtensionIsLowercaseWithoutDot()
    {
        var extensions = typeof(FileTypes)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(FileTypeFilter))
            .SelectMany(field => ((FileTypeFilter)field.GetValue(null)!).FlattenExtensions())
            .ToArray();

        Assert.That(extensions, Is.Not.Empty);
        Assert.That(extensions, Has.All.Matches<string>(extension => extension == extension.ToLowerInvariant() && !extension.Contains('.')));
    }

    [Test]
    public void OnlyFiltersWithReadableTextLookAtTheContent()
    {
        const string CONTENT = "resource \"example\" \"main\" {}";
        AssertPath(CONTENT, "infrastructure.tf", Encoding.UTF8, true);
        AssertPath(CONTENT, "infrastructure.tf", Encoding.UTF8, true, FileTypes.TEXT);
        AssertPath(CONTENT, "infrastructure.tf", Encoding.UTF8, false, FileTypes.SOURCE_CODE);
        AssertPath(CONTENT, "infrastructure.tf", Encoding.UTF8, false, FileTypes.AUDIO);
        AssertPath([0x01, 0x02, 0x03, 0x04, 0x05], "controls.unknown", false, FileTypes.DOCUMENT);
    }

    [Test]
    public void ReadableTextDoesNotClaimAnExtensionWithoutContent()
    {
        Assert.That(FileTypes.IsAllowedExtension("tf", FileTypes.READABLE_TEXT), Is.False);
    }

    [Test]
    public void ReadableTextDoesNotChangeExtensionFilters()
    {
        var combinedFilter = FileTypes.AsOneFileType(FileTypes.JSON, FileTypes.READABLE_TEXT);

        Assert.Multiple(() =>
        {
            Assert.That(FileTypes.AsOneFileType(FileTypes.READABLE_TEXT), Is.SameAs(FileTypes.READABLE_TEXT));
            Assert.That(combinedFilter, Is.Not.Null);
            Assert.That(combinedFilter!.FilterExtensions, Is.EqualTo(new[] { "json" }));
            Assert.That(FileTypes.AsOneFileType(FileTypes.JSON), Is.SameAs(FileTypes.JSON));
        });
    }

    [Test]
    public void ReadableTextIsAUniqueChildOfText()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileTypes.READABLE_TEXT.FilterName, Is.Not.EqualTo(FileTypes.TEXT.FilterName));
            Assert.That(FileTypes.TEXT.ContainsType(FileTypes.READABLE_TEXT), Is.True);
            Assert.That(FileTypes.DOCUMENT.Children, Does.Not.Contain(FileTypes.READABLE_TEXT));
            Assert.That(FileTypes.DOCUMENT.ContainsType(FileTypes.READABLE_TEXT), Is.True);
            Assert.That(FileTypes.AsOneFileType(FileTypes.DOCUMENT), Is.Not.Null);
        });

        AssertPath("resource \"example\" \"main\" {}", "infrastructure.tf", Encoding.UTF8, true, FileTypes.DOCUMENT);
    }

    [Test]
    public void LocalFileDataSourcesValidateTheSelectedFilesContents()
    {
        var validation = new DataSourceValidation();

        WithFile(
            Encoding.UTF8.GetBytes("resource \"example\" \"main\" {}"),
            "infrastructure.tf",
            path => Assert.That(validation.ValidateFilePath(path), Is.Null));
        WithFile(
            [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00],
            "archive.unknown",
            path => Assert.That(validation.ValidateFilePath(path), Is.Not.Null));
    }

    [TestCase("recording.mp3")]
    [TestCase("meeting.mp4")]
    [TestCase("voice-recording.webm")]
    public void LocalFileDataSourcesRejectMedia(string fileName)
    {
        var validation = new DataSourceValidation();
        WithFile([1, 2, 3], fileName, path => Assert.That(validation.ValidateFilePath(path), Is.Not.Null));
    }

    private static void AssertPath(string content, string fileName, Encoding encoding, bool expected, FileTypeFilter? type = null)
    {
        AssertPath(encoding.GetBytes(content), fileName, expected, type);
    }

    private static void AssertPath(byte[] content, string fileName, bool expected, FileTypeFilter? type = null)
    {
        WithFile(content, fileName, path =>
            Assert.That(FileTypes.IsAllowedPath(path, type ?? FileTypes.READABLE_TEXT), Is.EqualTo(expected)));
    }

    private static void WithFile(byte[] content, string fileName, Action<string> assertion)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ai-studio-file-types-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, fileName);
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllBytes(path, content);
            assertion(path);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
