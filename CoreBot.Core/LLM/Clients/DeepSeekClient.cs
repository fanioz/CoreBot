using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreBot.Core.Messages;

namespace CoreBot.Core.LLM.Clients;

/// <summary>
/// LLM provider client for DeepSeek
/// </summary>
public class DeepSeekClient : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public string ProviderName => "deepseek";

    public DeepSeekClient(HttpClient httpClient, string apiKey, string model)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        try
        {
            var requestBody = BuildRequestBody(request);

            var httpRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://api.deepseek.com/v1/chat/completions"),
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }),
                    Encoding.UTF8,
                    "application/json"
                )
            };

            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new LlmResponse
                {
                    Content = string.Empty,
                    Error = $"DeepSeek API error ({response.StatusCode}): {responseBody}"
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
            var choice = jsonResponse["choices"]?[0];

            if (choice == null)
            {
                return new LlmResponse
                {
                    Content = string.Empty,
                    Error = "No choices in response"
                };
            }

            var message = choice["message"];
            var content = message?["content"]?.ToString() ?? string.Empty;

            // Extract tool calls if present
            var toolCalls = ExtractToolCalls(message);

            // Extract usage info
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
                Error = $"DeepSeek request failed: {ex.Message}"
            };
        }
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamCompleteAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = request.Messages.Select(m => new
        {
            role = m.Role,
            content = m.Content,
            tool_calls = m.ToolCalls?.Select(tc => new
            {
                id = Guid.NewGuid().ToString(),
                type = "function",
                function = new
                {
                    name = tc.ToolName,
                    arguments = JsonSerializer.Serialize(tc.Parameters)
                }
            }).ToList()
        }).ToList();

        var requestBody = new
        {
            model = _model,
            messages = messages,
            tools = request.Tools?.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.Parameters
                }
            }).ToList(),
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            stop = request.Stop,
            stream = true
        };

        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.deepseek.com/v1/chat/completions"),
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                Encoding.UTF8,
                "application/json"
            )
        };

        httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _httpClient.SendAsync(httpRequest, ct);
        var stream = await response.Content.ReadAsStreamAsync(ct);

        using var reader = new StreamReader(stream);

        await foreach (var line in ReadLinesAsync(reader, ct))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("data: [DONE]"))
            {
                yield break;
            }

            if (line.StartsWith("data: "))
            {
                var json = line.Substring(6);
                var chunk = JsonSerializer.Deserialize<JsonObject>(json);

                if (chunk != null && chunk.TryGetPropertyValue("choices", out var choicesNode))
                {
                    var choices = choicesNode.AsArray();
                    if (choices != null && choices.Count > 0)
                    {
                        var choice = choices[0];
                        var delta = choice?["delta"];

                        if (delta != null)
                        {
                            var contentNode = delta["content"];
                            if (contentNode != null)
                            {
                                var content = contentNode.ToString() ?? string.Empty;
                                yield return new LlmStreamChunk
                                {
                                    Content = content,
                                    IsFinal = false
                                };
                            }
                        }
                    }
                }
            }
        }
    }

    private object BuildRequestBody(LlmRequest request)
    {
        var messages = request.Messages.Select(m => new
        {
            role = m.Role,
            content = m.Content,
            tool_calls = m.ToolCalls?.Select(tc => new
            {
                id = Guid.NewGuid().ToString(),
                type = "function",
                function = new
                {
                    name = tc.ToolName,
                    arguments = JsonSerializer.Serialize(tc.Parameters)
                }
            }).ToList(),
            tool_call_id = m.ToolResult?.MessageId
        }).ToList();

        return new
        {
            model = _model,
            messages = messages,
            tools = request.Tools?.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.Parameters
                }
            }).ToList(),
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            stop = request.Stop
        };
    }

    private List<ToolCall>? ExtractToolCalls(JsonNode? messageNode)
    {
        if (messageNode == null) return null;

        var toolCalls = messageNode["tool_calls"];
        if (toolCalls == null) return null;

        var calls = new List<ToolCall>();
        foreach (var call in toolCalls.AsArray())
        {
            var function = call["function"];
            if (function != null)
            {
                var argumentsStr = function["arguments"]?.ToString() ?? "{}";
                calls.Add(new ToolCall(
                    function["name"]?.ToString() ?? "",
                    JsonDocument.Parse(argumentsStr).RootElement
                ));
            }
        }

        return calls.Count > 0 ? calls : null;
    }

    private LlmUsage? ExtractUsage(JsonNode jsonResponse)
    {
        var usage = jsonResponse["usage"];
        if (usage == null) return null;

        return new LlmUsage
        {
            PromptTokens = usage["prompt_tokens"]?.GetValue<int>() ?? 0,
            CompletionTokens = usage["completion_tokens"]?.GetValue<int>() ?? 0
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
