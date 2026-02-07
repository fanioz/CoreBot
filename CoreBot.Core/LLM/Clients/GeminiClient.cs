using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreBot.Core.Messages;

namespace CoreBot.Core.LLM.Clients;

/// <summary>
/// LLM provider client for Google Gemini
/// </summary>
public class GeminiClient : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public string ProviderName => "gemini";

    public GeminiClient(HttpClient httpClient, string apiKey, string model)
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
                RequestUri = new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}"),
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }),
                    Encoding.UTF8,
                    "application/json"
                )
            };

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new LlmResponse
                {
                    Content = string.Empty,
                    Error = $"Gemini API error ({response.StatusCode}): {responseBody}"
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

            var candidate = jsonResponse["candidates"]?[0];
            if (candidate == null)
            {
                return new LlmResponse
                {
                    Content = string.Empty,
                    Error = "No candidates in response"
                };
            }

            var content = candidate["content"];
            var textParts = content?["parts"]?.AsArray();
            var text = textParts != null && textParts.Count > 0
                ? string.Join("", textParts.Select(p => p["text"]?.ToString() ?? ""))
                : string.Empty;

            // Extract function calls
            var functionCalls = ExtractFunctionCalls(content);

            // Extract usage
            var usage = ExtractUsage(candidate);

            return new LlmResponse
            {
                Content = text,
                ToolCalls = functionCalls,
                Usage = usage,
                WasStreamed = false
            };
        }
        catch (Exception ex)
        {
            return new LlmResponse
            {
                Content = string.Empty,
                Error = $"Gemini request failed: {ex.Message}"
            };
        }
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamCompleteAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var requestBody = BuildRequestBody(request);

        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{_model}:streamGenerateContent?key={_apiKey}"),
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                Encoding.UTF8,
                "application/json"
            )
        };

        var response = await _httpClient.SendAsync(httpRequest, ct);
        var stream = await response.Content.ReadAsStreamAsync(ct);

        using var reader = new StreamReader(stream);

        await foreach (var line in ReadLinesAsync(reader, ct))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chunk = JsonSerializer.Deserialize<JsonObject>(line);
            if (chunk == null) continue;

            var candidate = chunk["candidates"]?[0];
            if (candidate == null) continue;

            var content = candidate["content"];
            if (content == null) continue;

            var parts = content["parts"]?.AsArray();
            if (parts != null && parts.Count > 0)
            {
                var text = parts.Select(p => p["text"]?.ToString() ?? "")
                               .Aggregate("", (a, b) => a + b);

                if (!string.IsNullOrEmpty(text))
                {
                    yield return new LlmStreamChunk
                    {
                        Content = text,
                        IsFinal = false
                    };
                }
            }
        }
    }

    private object BuildRequestBody(LlmRequest request)
    {
        var contents = request.Messages.Select(m =>
        {
            string role = m.Role switch
            {
                "user" => "user",
                "assistant" => "model",
                "system" => "user", // Gemini treats system messages as user messages
                "tool" => "user",
                _ => "user"
            };

            var parts = new List<object>();

            // Add text content
            if (!string.IsNullOrWhiteSpace(m.Content))
            {
                parts.Add(new { text = m.Content });
            }

            // Add function response for tool results
            if (m.ToolResult != null)
            {
                parts.Add(new
                {
                    functionResponse = new
                    {
                        name = m.ToolResult.ToolName,
                        response = new
                        {
                            content = m.ToolResult.Result
                        }
                    }
                });
            }

            // Add function calls for assistant tool calls
            if (m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                foreach (var tc in m.ToolCalls)
                {
                    var args = JsonSerializer.Serialize(tc.Parameters);
                    var argsObj = JsonSerializer.Deserialize<JsonObject>(args);

                    parts.Add(new
                    {
                        functionCall = new
                        {
                            name = tc.ToolName,
                            args = argsObj
                        }
                    });
                }
            }

            return new
            {
                role = role,
                parts = parts
            };
        }).ToList();

        var tools = request.Tools?.Select(t => new
        {
            functionDeclarations = new[]
            {
                new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.Parameters
                }
            }
        }).ToList();

        return new
        {
            contents = contents,
            tools = tools,
            generationConfig = new
            {
                maxOutputTokens = request.MaxTokens,
                temperature = request.Temperature,
                stopSequences = request.Stop
            }
        };
    }

    private List<ToolCall>? ExtractFunctionCalls(JsonNode? contentNode)
    {
        if (contentNode == null) return null;

        var parts = contentNode["parts"]?.AsArray();
        if (parts == null) return null;

        var toolCalls = new List<ToolCall>();

        foreach (var part in parts)
        {
            var functionCall = part["functionCall"];
            if (functionCall != null)
            {
                var name = functionCall["name"]?.ToString() ?? "";
                var args = functionCall["args"];

                var argsJson = args != null
                    ? JsonSerializer.Serialize(args)
                    : "{}";

                toolCalls.Add(new ToolCall(name, JsonDocument.Parse(argsJson).RootElement));
            }
        }

        return toolCalls.Count > 0 ? toolCalls : null;
    }

    private LlmUsage? ExtractUsage(JsonNode candidate)
    {
        var usage = candidate["usageMetadata"];
        if (usage == null) return null;

        return new LlmUsage
        {
            PromptTokens = usage["promptTokenCount"]?.GetValue<int>() ?? 0,
            CompletionTokens = usage["candidatesTokenCount"]?.GetValue<int>() ?? 0
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
