using CoreBot.Core.Tools;

namespace CoreBot.Core.LLM;

/// <summary>
/// Interface for LLM provider clients
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Provider name (e.g., "openrouter", "anthropic", "openai")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Get a completion from the LLM
    /// </summary>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>
    /// Stream a completion from the LLM
    /// </summary>
    IAsyncEnumerable<LlmStreamChunk> StreamCompleteAsync(LlmRequest request, CancellationToken ct = default);
}
