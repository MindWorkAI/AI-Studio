using AIStudio.Tools.Services;

namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Loads the attached pictures through the runtime.
/// </summary>
/// <remarks>
/// The runtime checks that each file is the image its extension claims, and scales large pictures
/// down, so that a presentation full of photos stays small enough to send by mail.
/// </remarks>
/// <param name="rustService">The runtime.</param>
/// <param name="picturePaths">The paths of the pictures the slides use, by the numbers the model gave them.</param>
/// <param name="iconNumbers">The numbers of the pictures which are icons of a picture library.</param>
internal sealed class RustPictureSource(RustService rustService, IReadOnlyDictionary<int, string> picturePaths, IReadOnlySet<int> iconNumbers) : IPictureSource
{
    /// <inheritdoc />
    public async Task<SlidePicture?> LoadAsync(int number, CancellationToken token)
    {
        if (!picturePaths.TryGetValue(number, out var picturePath))
            return null;

        try
        {
            var prepared = await rustService.PrepareImageAsync(picturePath, true, token);
            var dataStart = prepared.DataUrl.IndexOf(',');
            if (dataStart < 0)
                return null;

            return new SlidePicture(Convert.FromBase64String(prepared.DataUrl[(dataStart + 1)..]), prepared.MimeType, (int)prepared.Width, (int)prepared.Height, iconNumbers.Contains(number));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The slot stays empty, and the writer reports the picture as skipped:
            return null;
        }
    }
}