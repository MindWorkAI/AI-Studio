using System.Net.Http.Headers;

namespace AIStudio.Provider;

/// <summary>
/// The headers which tell a gateway that a request comes from AI Studio.
/// </summary>
/// <remarks>
/// OpenRouter and Requesty read the same two headers: "HTTP-Referer" for the address of the app,
/// and "X-Title" for its name. OpenRouter lists apps by them in its rankings (App Attribution), and
/// Requesty tags the analytics of its users with them (Origin Headers), so a person can tell the
/// requests of AI Studio apart from those of their other tools. OpenRouter calls the second header
/// "X-OpenRouter-Title" by now and still reads "X-Title"; staying with the name both gateways
/// understand is what lets them share this.
///
/// Neither header says anything about the user: both values are the same for every installation.
/// </remarks>
public static class ProjectAttribution
{
    /// <summary>
    /// The address the gateways identify the app by.
    /// </summary>
    private const string PROJECT_WEBSITE = "https://github.com/MindWorkAI/AI-Studio";

    /// <summary>
    /// The name the gateways show for the app.
    /// </summary>
    private const string PROJECT_NAME = "MindWork AI Studio";

    /// <summary>
    /// Adds the attribution headers to a request.
    /// </summary>
    /// <param name="headers">The headers of the request.</param>
    public static void Apply(HttpRequestHeaders headers)
    {
        headers.Add("HTTP-Referer", PROJECT_WEBSITE);
        headers.Add("X-Title", PROJECT_NAME);
    }
}