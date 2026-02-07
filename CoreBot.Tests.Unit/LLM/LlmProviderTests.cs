using System.Text.Json;
using CoreBot.Core.LLM;
using CoreBot.Core.LLM.Clients;
using Xunit;

namespace CoreBot.Tests.Unit.LLM;

/// <summary>
/// Unit tests for LLM providers
/// </summary>
public class LlmProviderTests
{
    [Fact]
    public void OpenRouterClient_ProviderName_IsCorrect()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new OpenRouterClient(httpClient, "test-key", "test-model");

        // Assert
        Assert.Equal("openrouter", client.ProviderName);
    }

    [Fact]
    public void AnthropicClient_ProviderName_IsCorrect()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new AnthropicClient(httpClient, "test-key", "test-model");

        // Assert
        Assert.Equal("anthropic", client.ProviderName);
    }

    [Fact]
    public void OpenAIClient_ProviderName_IsCorrect()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new OpenAIClient(httpClient, "test-key", "test-model");

        // Assert
        Assert.Equal("openai", client.ProviderName);
    }

    [Fact]
    public void DeepSeekClient_ProviderName_IsCorrect()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new DeepSeekClient(httpClient, "test-key", "test-model");

        // Assert
        Assert.Equal("deepseek", client.ProviderName);
    }

    [Fact]
    public void GroqClient_ProviderName_IsCorrect()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new GroqClient(httpClient, "test-key", "test-model");

        // Assert
        Assert.Equal("groq", client.ProviderName);
    }

    [Fact]
    public void GeminiClient_ProviderName_IsCorrect()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new GeminiClient(httpClient, "test-key", "test-model");

        // Assert
        Assert.Equal("gemini", client.ProviderName);
    }

    [Fact]
    public void LlmRequest_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var request = new LlmRequest();

        // Assert
        Assert.NotNull(request.Messages);
        Assert.Empty(request.Messages);
        Assert.Null(request.Tools);
        Assert.Equal(4096, request.MaxTokens);
        Assert.Equal(0.7, request.Temperature);
        Assert.Null(request.Stop);
    }

    [Fact]
    public void LlmResponse_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var response = new LlmResponse();

        // Assert
        Assert.Equal(string.Empty, response.Content);
        Assert.Null(response.ToolCalls);
        Assert.False(response.WasStreamed);
        Assert.Null(response.Error);
        Assert.Null(response.Usage);
    }

    [Fact]
    public void LlmUsage_TotalTokens_CalculatesCorrectly()
    {
        // Arrange & Act
        var usage = new LlmUsage
        {
            PromptTokens = 100,
            CompletionTokens = 50
        };

        // Assert
        Assert.Equal(150, usage.TotalTokens);
    }

    [Fact]
    public void LlmStreamChunk_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var chunk = new LlmStreamChunk();

        // Assert
        Assert.Equal(string.Empty, chunk.Content);
        Assert.Null(chunk.ToolCalls);
        Assert.False(chunk.IsFinal);
    }
}
