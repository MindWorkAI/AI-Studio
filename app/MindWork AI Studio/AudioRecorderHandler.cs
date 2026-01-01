using AIStudio.Tools.Services;

namespace AIStudio;

public static class AudioRecorderHandler
{
    public static void AddAudioRecorderHandlers(this IEndpointRouteBuilder app)
    {
        var router = app.MapGroup("/audio");
        
        router.MapPost("/upload", UploadAudio)
            .DisableAntiforgery();
    }

    private static async Task<IResult> UploadAudio(IFormFile audio, RustService rustService)
    {
        if (audio.Length == 0)
            return Results.BadRequest();

        var dataDirectory = await rustService.GetDataDirectory();
        var recordingDirectory = Path.Combine(dataDirectory, "audioRecordings");
        if(!Path.Exists(recordingDirectory))
            Directory.CreateDirectory(recordingDirectory);
        
        var fileName = $"recording_{DateTime.UtcNow:yyyyMMdd_HHmmss}.webm";
        var filePath = Path.Combine(recordingDirectory, fileName);
        
        await using var stream = File.Create(filePath);
        await audio.CopyToAsync(stream);

        return Results.Ok(new { FileName = fileName });
    }
}