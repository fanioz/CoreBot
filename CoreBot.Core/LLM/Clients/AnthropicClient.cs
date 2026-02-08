using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreBot.Core.Messages;

namespace CoreBot.Core.LLM.Clients;

/// <summary>
/// LLM provider client for Anthropic Claude
/// </summary>
public class AnthropicClient : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public string ProviderName => "anthropic";

    public AnthropicClient(HttpClient httpClient, string apiKey, string model, string? baseUrl = null)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _baseUrl = baseUrl ?? "https://api.anthropic.com";
    }

    private string GetApiPath() => $"{_baseUrl.TrimEnd('/')}/v1/messages";

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        try
        {
            var (messages, system) = SeparateSystemMessage(request);
            var requestBody = BuildRequestBody(messages, system, request);

            var httpRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(GetApiPath()),
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }),
                    Encoding.UTF8,
                    "application/json"
                )
            };

            httpRequest.Headers.Add("x-api-key", _apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new LlmResponse
                {
                    Content = string.Empty,
                    Error = $"Anthropic API error ({response.StatusCode}): {responseBody}"
                };
            }

            var jsonResponse = JsonNode.Parse(responseBody);
            if (jsonResponse == null)
            {
                return new LlmResponse
                {
                    Content = string.Empty,
                    Error = "Failed to parse response"
                };
            }

            // Check for errors
            var errorNode = jsonResponse["error"];
            if (errorNode != null)
            {
                return new LlmResponse
                {
                    Content = string.Empty,
                    Error = errorNode["message"]?.ToString() ?? "Unknown error"
                };
            }

            // Extract content from array
            var contentArray = jsonResponse["content"]?.AsArray();
            var textBlocks = contentArray?.Where(node => node?["type"]?.ToString() == "text")
                                        .Select(node => node?["text"]?.ToString() ?? "")
                                        .ToArray() ?? Array.Empty<string>();
            var content = string.Join("", textBlocks);

            // Extract tool calls
            var stopReason = jsonResponse["stop_reason"]?.ToString();
            var toolCalls = stopReason == "tool_use" ? ExtractToolCalls(jsonResponse) : null;

            // Extract usage
            var usage = ExtractUsage(jsonResponse);

            return new LlmResponse
            {
                Content = content,
                ToolCalls = toolCalls,
                Usage = usage,
                WasStreamed = false
            };
        }
        catch (Exception ex)
        {
            return new LlmResponse
            {
                Content = string.Empty,
                Error = $"Anthropic request failed: {ex.Message}"
            };
        }
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamCompleteAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var (messages, system) = SeparateSystemMessage(request);

        var tools = request.Tools?.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            input_schema = t.Parameters
        }).ToList();

        var requestBody = new
        {
            model = _model,
            messages = messages,
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            stop = request.Stop,
            tools = tools.Count > 0 ? tools : null,
            system = system,
            stream = true
        };

        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(GetApiPath()),
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                Encoding.UTF8,
                "application/json"
            )
        };

        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(httpRequest, ct);
        var stream = await response.Content.ReadAsStreamAsync(ct);

        using var reader = new StreamReader(stream);

        await foreach (var line in ReadLinesAsync(reader, ct))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("data: "))
            {
                var json = line.Substring(6);

                if (json == "[DONE]")
                {
                    yield return new LlmStreamChunk
                    {
                        Content = "",
                        IsFinal = true
                    };
                    break;
                }

                var chunk = JsonSerializer.Deserialize<JsonObject>(json);
                if (chunk == null) continue;

                if (chunk.TryGetPropertyValue("type", out var typeNode))
                {
                    var type = typeNode?.ToString();

                    if (type == "content_block_start")
                    {
                        // Start of content block
                    }
                    else if (type == "content_block_delta")
                    {
                        // Content delta
                        var text = chunk["text"]?.ToString() ?? string.Empty;
                        yield return new LlmStreamChunk
                        {
                            Content = text,
                            IsFinal = false
                        };
                    }
                    else if (type == "content_block_stop")
                    {
                        // End of content block
                    }
                }
            }
        }
    }

    private (List<object> messages, string? system) SeparateSystemMessage(LlmRequest request)
    {
        var system = request.Messages.FirstOrDefault(m => m.Role == "system")?.Content;
        var messages = request.Messages.Where(m => m.Role != "system").ToList();

        // Convert CoreBot message format to Anthropic format
        var anthropicMessages = messages.Select(m => (object)(m.Role switch
        {
            "user" => new { role = "user", content = m.Content },
            "assistant" => HandleAssistantMessage(m),
            "tool" => new
            {
                role = "user",
                content = new
                {
                    type = "tool_result",
                    tool_use_id = m.ToolResult?.MessageId ?? "",
                    content = m.ToolResult?.Result ?? ""
                }
            },
            _ => new { role = "user", content = m.Content }
        })).ToList();

        return (anthropicMessages, system);
    }

    private static object HandleAssistantMessage(LlmMessage m)
    {
        // If there are tool calls, return tool_use blocks
        if (m.ToolCalls != null && m.ToolCalls.Count > 0)
        {
            var toolUseContent = m.ToolCalls.Select(tc => new
            {
                id = Guid.NewGuid().ToString(),
                type = "tool_use",
                name = tc.ToolName,
                input = JsonSerializer.Serialize(tc.Parameters)
            }).ToList();

            return new { role = "assistant", content = toolUseContent };
        }
        else if (m.ToolResult != null)
        {
            return new
            {
                role = "user",
                content = new[]
                {
                    new
                    {
                        type = "tool_result",
                        tool_use_id = m.ToolResult.MessageId,
                        content = m.ToolResult.Result
                    }
                }
            };
        }

        // Regular text response - must be in array format
        var textBlock = new { type = "text", text = m.Content };
        return new { role = "assistant", content = new[] { textBlock } };
    }

    private object BuildRequestBody(List<object> messages, string? system, LlmRequest request)
    {
        var tools = request.Tools?.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            input_schema = t.Parameters
        }).ToList();

        var body = new
        {
            model = _model,
            messages = messages,
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            stop = request.Stop,
            tools = tools.Count > 0 ? tools : null,
            system = system
        };

        return body;
    }

    private List<ToolCall>? ExtractToolCalls(JsonNode jsonResponse)
    {
        var content = jsonResponse["content"];
        if (content == null) return null;

        var blocks = content.AsArray();
        if (blocks == null) return null;

        var toolCalls = new List<ToolCall>();

        foreach (var block in blocks)
        {
            if (block["type"]?.ToString() == "tool_use")
            {
                var id = block["id"]?.ToString() ?? "";
                var name = block["name"]?.ToString() ?? "";
                var input = block["input"]?.ToString() ?? "{}";

                toolCalls.Add(new ToolCall(name, JsonDocument.Parse(input).RootElement));
            }
        }

        return toolCalls.Count > 0 ? toolCalls : null;
    }

    private LlmUsage? ExtractUsage(JsonNode jsonResponse)
    {
        var usage = jsonResponse["usage"];
        if (usage == null) return null;

        return new LlmUsage
        {
            PromptTokens = usage["input_tokens"]?.GetValue<int>() ?? 0,
            CompletionTokens = usage["output_tokens"]?.GetValue<int>() ?? 0
        };
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(StreamReader reader, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            yield return line;
        }
    }
}
