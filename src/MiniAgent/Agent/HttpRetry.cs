using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace MiniAgent.Agent;

internal static class HttpRetry
{
    private const int MaxAttempts = 3;

    /// <summary>POST JSON; retries on 429 and 5xx with exponential backoff.</summary>
    public static async Task<JsonNode> PostJsonAsync(
        HttpClient http,
        string url,
        JsonNode body,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct)
    {
        var payload = body.ToJsonString();

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            foreach (var header in headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            using var response = await http.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
                return JsonNode.Parse(raw) ?? throw new InvalidOperationException("Empty response from LLM API.");

            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests
                            || (int)response.StatusCode >= 500;
            if (!retryable || attempt >= MaxAttempts)
                throw new HttpRequestException($"LLM API error {(int)response.StatusCode}: {raw}");

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }
    }
}
