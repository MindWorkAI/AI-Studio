using System.Net.Http.Headers;

namespace AIStudio.Tools;

public static class HttpRequestHeadersExtensions
{
    private static readonly string API_TOKEN;

    static HttpRequestHeadersExtensions()
    {
        API_TOKEN = Program.API_TOKEN;
    }
    
    public static void AddApiToken(this HttpRequestHeaders headers)
    {
        headers.Add("token", API_TOKEN);
    }
}