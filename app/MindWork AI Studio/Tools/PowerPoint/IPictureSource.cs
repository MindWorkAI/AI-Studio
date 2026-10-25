namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Hands out the pictures a slide deck refers to by number.
/// </summary>
internal interface IPictureSource
{
    /// <summary>
    /// Loads a picture.
    /// </summary>
    /// <param name="number">The 1-based number the model used for the picture.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The picture, or null when there is no such picture or it cannot be read.</returns>
    public Task<SlidePicture?> LoadAsync(int number, CancellationToken token);
}