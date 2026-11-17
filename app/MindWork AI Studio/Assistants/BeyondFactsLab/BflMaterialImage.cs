namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The copy of an image which the lab keeps as material.
/// </summary>
/// <remarks>
/// The copy is prepared for models when the person adds the image: turned upright, at most 2,560 pixels along each
/// edge, as JPEG or PNG, and without metadata such as the place where a photo was taken. It lies beside the record
/// of the material as materials/ID.EXTENSION.
/// </remarks>
public sealed class BflMaterialImage
{
    /// <summary>
    /// The file extensions a copy may have; the runtime tells the format of an image by its extension.
    /// </summary>
    public static readonly IReadOnlySet<string> EXTENSIONS = new HashSet<string>(StringComparer.Ordinal) { "jpg", "png" };

    /// <summary>
    /// Gets or sets the file extension of the copy, without the dot, e.g., "jpg".
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the width of the copy in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the copy in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Checks whether the description of a copy is complete: a known extension and a size.
    /// </summary>
    /// <param name="image">The description, or null.</param>
    /// <returns>True when the description is complete; false for null.</returns>
    public static bool IsValid(BflMaterialImage? image) => image is { Width: > 0, Height: > 0 } && EXTENSIONS.Contains(image.Extension);
}