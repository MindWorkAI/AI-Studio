using System.Net.Http.Headers;

namespace AIStudio.Tools;

public static class HttpRequestHeadersExtensions
{
    private static readonly string API_TOKEN;

    static HttpRequestHeadersExtensions()
    {
        API_TOKEN = Program.API_TOKEN;
    }
    
    extension(HttpRequestHeaders headers)
    {
        public void AddApiToken()
        {
            headers.Add("token", API_TOKEN);
        }
    }
}