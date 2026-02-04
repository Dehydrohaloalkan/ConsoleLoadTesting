using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace JsonPostClient.Services;

public static class PostService
{
    public static async Task<PostResult> SendJsonAsync(HttpClient client, string url, string jsonContent, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

            var response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return new PostResult
            {
                StatusCode = (int)response.StatusCode,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new PostResult
            {
                StatusCode = 0,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }
}

public record PostResult
{
    public int StatusCode { get; init; }
    public long ResponseTimeMs { get; init; }
    public string? ErrorMessage { get; init; }
}
