using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks which pictures of a library folder the LLM may choose from.
/// </summary>
[TestFixture]
public sealed class PictureLibraryTests
{
    private string folder = string.Empty;

    [SetUp]
    public void CreateFolder()
    {
        this.folder = Path.Combine(Path.GetTempPath(), $"ai-studio-picture-library-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.folder);
    }

    [TearDown]
    public void DeleteFolder() => Directory.Delete(this.folder, true);

    [Test]
    public void OnlyPngAndJpegPicturesCountAndIconsGoByTheirNames()
    {
        this.CreateFiles("rocket.png", "Moon.JPG", "mars.jpeg", "star.svg", "notes.txt", ".hidden.png", "icon-star.png", "icons/flag.png", "IconSet/small/planet.png", "photos/crater.png");

        var library = PictureLibrary.Read(this.folder, new Random(1), PictureLibrary.MAX_LIBRARY_PICTURES, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(library.Pictures.Select(picture => picture.Name), Is.EquivalentTo(new[] { "rocket.png", "Moon.JPG", "mars.jpeg", "icon-star.png", "icons/flag.png", "IconSet/small/planet.png", "photos/crater.png" }), "Hidden files stay out, as everywhere in AI Studio.");
            Assert.That(library.Pictures.Select(picture => picture.Name), Is.Ordered.Using<string>(StringComparer.OrdinalIgnoreCase));
            Assert.That(library.Pictures.Where(picture => picture.IsIcon).Select(picture => picture.Name), Is.EquivalentTo(new[] { "icon-star.png", "icons/flag.png", "IconSet/small/planet.png" }), "A file or any of its folders may say that it is an icon.");
            Assert.That(library.PictureCount, Is.EqualTo(7));
            Assert.That(library.IconCount, Is.EqualTo(3));
            Assert.That(library.SkippedFileCount, Is.EqualTo(2), "The SVG icon and the notes are neither PNG nor JPEG.");
            Assert.That(library.IsSampled, Is.False);
        });
    }

    [Test]
    public void AFolderOfIconsMakesAllOfItsPicturesIcons()
    {
        //
        // The library folder counts like the folders below it, so a folder "Icons" holds icons only.
        // The folders above it say where the library lies, so a library inside "iconography" gains nothing:
        //
        var iconFolder = Path.Combine(this.folder, "Icons");
        var iconSetFolder = Path.Combine(this.folder, "IconSet");
        var photoFolder = Path.Combine(this.folder, "iconography", "Photos");
        this.CreateFiles("Icons/rocket.png", "Icons/planets/mars.png", "IconSet/moon.jpg", "iconography/Photos/crater.png");

        Assert.Multiple(() =>
        {
            Assert.That(PictureLibrary.Read(iconFolder, new Random(1), PictureLibrary.MAX_LIBRARY_PICTURES, null, CancellationToken.None).Pictures.Select(picture => picture.IsIcon), Is.EqualTo(new[] { true, true }));
            Assert.That(PictureLibrary.Read(iconSetFolder + Path.DirectorySeparatorChar, new Random(1), PictureLibrary.MAX_LIBRARY_PICTURES, null, CancellationToken.None).IconCount, Is.EqualTo(1), "A trailing separator changes nothing.");
            Assert.That(PictureLibrary.Read(photoFolder, new Random(1), PictureLibrary.MAX_LIBRARY_PICTURES, null, CancellationToken.None).IconCount, Is.Zero);
        });
    }

    [Test]
    public void ALargeLibraryIsSampledAtRandom()
    {
        this.CreateFiles([..Enumerable.Range(1, 30).Select(number => $"picture-{number:00}.png")]);

        var sample = PictureLibrary.Read(this.folder, new Random(7), 10, null, CancellationToken.None);
        var again = PictureLibrary.Read(this.folder, new Random(7), 10, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(sample.Pictures, Has.Count.EqualTo(10));
            Assert.That(sample.Pictures.Select(picture => picture.Name), Is.Unique);
            Assert.That(sample.Pictures.Select(picture => picture.Name), Is.Ordered.Using<string>(StringComparer.OrdinalIgnoreCase), "The sample is sorted after it is drawn.");
            Assert.That(sample.PictureCount, Is.EqualTo(30), "The count tells about the whole library.");
            Assert.That(sample.IsSampled, Is.True);
            Assert.That(again.Pictures.Select(picture => picture.Name), Is.EqualTo(sample.Pictures.Select(picture => picture.Name)), "The same random numbers draw the same sample.");
        });
    }

    [Test]
    public void EveryPictureHasTheSameChanceToBeInTheSample()
    {
        //
        // The first pictures fill the sample, so a sampling which only kept them would let the
        // last pictures never in. Drawn a thousand times, every picture shows up about as often:
        //
        this.CreateFiles([..Enumerable.Range(1, 20).Select(number => $"picture-{number:00}.png")]);
        var random = new Random(3);
        var counts = new Dictionary<string, int>();
        for (var draw = 0; draw < 1_000; draw++)
        {
            foreach (var picture in PictureLibrary.Read(this.folder, random, 5, null, CancellationToken.None).Pictures)
                counts[picture.Name] = counts.GetValueOrDefault(picture.Name) + 1;
        }

        // Each picture is in a sample with a chance of 5 in 20, i.e., about 250 times:
        Assert.That(counts.Values, Has.All.InRange(180, 320));
        Assert.That(counts, Has.Count.EqualTo(20));
    }

    private void CreateFiles(params string[] names)
    {
        foreach (var name in names)
        {
            var path = Path.Combine(this.folder, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [1, 2, 3]);
        }
    }
}
