using System.Text.Json;
using System.Net.Http;
using System.Text;

namespace CoreBot.Core.Tools.BuiltIn;

/// <summary>
/// Tool for fetching web pages via HTTP/HTTPS
/// </summary>
public class WebFetchTool : IToolDefinition
{
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public WebFetchTool(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "web_fetch";

    public string Description => "Fetch a web page via HTTP/HTTPS";

    public JsonDocument GetSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                url = new
                {
                    type = "string",
                    description = "URL to fetch"
                }
            },
            required = new[] { "url" }
        };

        return JsonDocument.Parse(JsonSerializer.Serialize(schema));
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        try
        {
            // Extract parameters
            if (!parameters.TryGetProperty("url", out var urlElement))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = "Missing required parameter: url"
                };
            }

            var url = urlElement.GetString();
            if (string.IsNullOrWhiteSpace(url))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = "URL parameter cannot be empty"
                };
            }

            // Validate URL format
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = $"Invalid URL format: {url}"
                };
            }

            // Validate scheme
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = $"URL must use HTTP or HTTPS scheme: {url}"
                };
            }

            // Create cancellation token with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);

            // Fetch the page
            var response = await _httpClient.GetAsync(uri, cts.Token);

            var content = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            // Try to decode as UTF-8, fall back to ASCII if needed
            string decodedContent;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                decodedContent = Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                decodedContent = content;
            }

            return new ToolResult
            {
                Success = true,
                Result = decodedContent
            };
        }
        catch (OperationCanceledException)
        {
            return new ToolResult
            {
                Success = false,
                Result = string.Empty,
                Error = $"Request timed out after {_timeout.TotalSeconds} seconds"
            };
        }
        catch (HttpRequestException ex)
        {
            return new ToolResult
            {
                Success = false,
                Result = string.Empty,
                Error = $"HTTP request failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Result = string.Empty,
                Error = $"Failed to fetch URL: {ex.Message}"
            };
        }
    }
}
