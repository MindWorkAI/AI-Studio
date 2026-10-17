using System.Text;

using AIStudio.Tools.Rust;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools;

public class FileTypesTests
{
    [TestCase("main.tf", "resource \"example\" \"main\" { description = \"Grüße\" }")]
    [TestCase(".gitignore", "bin/\nobj/\n")]
    [TestCase("LICENSE", "Permission is hereby granted.")]
    [TestCase("Dockerfile", "FROM alpine:latest")]
    [TestCase("Dockerfile.development", "FROM alpine:latest")]
    [TestCase("Makefile", "build:\n\tdotnet build")]
    [TestCase(".env", "APP_ENV=development")]
    [TestCase("deployment notes (final) [β] #1", "Unusually named UTF-8 text file")]
    public void ReadableTextAcceptsUtf8PathsWithoutAKnownExtension(string fileName, string content)
    {
        AssertPath(content, fileName, Encoding.UTF8, true);
    }

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

    [Test]
    public void ReadableTextAcceptsLegacySingleByteText()
    {
        AssertPath([0x47, 0x72, 0xFC, 0xDF, 0x65], "greeting.data", true);
    }

    [Test]
    public void ReadableTextAcceptsBomMarkedUnicode()
    {
        var content = Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes("A UTF-16 text file."))
            .ToArray();

        AssertPath(content, "utf16.data", true);
    }

    [Test]
    public void ReadableTextRejectsUtf32WhichTheRuntimeTextReaderDoesNotSupport()
    {
        var content = Encoding.UTF32.GetPreamble()
            .Concat(Encoding.UTF32.GetBytes("UTF-32 text"))
            .ToArray();

        AssertPath(content, "utf32.data", false);
    }

    [Test]
    public void ReadableTextRejectsBinaryContent()
    {
        AssertPath([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00], "archive.unknown", false);
        AssertPath([0x01, 0x02, 0x03, 0x04, 0x05], "controls.unknown", false);
        AssertPath([0x01, 0x02, 0x03, 0x04, 0x05], "controls.unknown", false, FileTypes.DOCUMENT);
    }

    [Test]
    public void ReadableTextRejectsAnUnreadablePath()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"ai-studio-missing-{Guid.NewGuid():N}.tf");
        Assert.That(FileTypes.IsAllowedPath(missingPath, FileTypes.READABLE_TEXT), Is.False);
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
