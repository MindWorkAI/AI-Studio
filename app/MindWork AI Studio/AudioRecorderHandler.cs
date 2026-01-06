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

    private static async Task<IResult> UploadAudio(HttpRequest request, RustService rustService)
    {
        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("audio");
        var mimeType = form["mimeType"].ToString();
        
        if (file is null || file.Length == 0)
            return Results.BadRequest("No audio file uploaded.");

        var actualMimeType = !string.IsNullOrWhiteSpace(mimeType) 
            ? mimeType 
            : file.ContentType;
    
        var extension = GetFileExtension(actualMimeType);
        
        var dataDirectory = await rustService.GetDataDirectory();
        var recordingDirectory = Path.Combine(dataDirectory, "audioRecordings");
        if(!Path.Exists(recordingDirectory))
            Directory.CreateDirectory(recordingDirectory);
        
        var fileName = $"recording_{DateTime.UtcNow:yyyyMMdd_HHmmss}{extension}";
        var filePath = Path.Combine(recordingDirectory, fileName);
        
        await using var stream = File.Create(filePath);
        await file.CopyToAsync(stream);

        return Results.Ok(new 
        { 
            FileName = fileName, 
            MimeType = actualMimeType,
            Size = file.Length 
        });
    }
    
    static string GetFileExtension(string mimeType)
    {
        var baseMimeType = mimeType.Split(';')[0].Trim().ToLowerInvariant();
    
        return baseMimeType switch
        {
            "audio/webm" => ".webm",
            "audio/ogg" => ".ogg",
            "audio/mp4" => ".m4a",
            "audio/mpeg" => ".mp3",
            "audio/wav" => ".wav",
            "audio/x-wav" => ".wav",
            "audio/aac" => ".aac",
            _ => ".audio"
        };
    }
}