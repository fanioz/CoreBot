using System.Text.Json;
using CoreBot.Core.Messaging;
using CoreBot.Core.Messages;

namespace CoreBot.Core.ChatPlatforms;

/// <summary>
/// CLI interface for direct interaction with CoreBot
/// </summary>
public class CliAdapter
{
    private readonly IMessageBus _messageBus;
    private readonly CliConfiguration _config;

    public CliAdapter(CliConfiguration config, IMessageBus messageBus)
    {
        _config = config;
        _messageBus = messageBus;
    }

    /// <summary>
    /// Send a message to the agent and wait for response
    /// </summary>
    public async Task<string> SendMessageAsync(string message, CancellationToken ct = default)
    {
        var userId = _config.UserId ?? "cli-user";

        // Create user message
        var userMessage = new UserMessage(
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            "cli",
            userId,
            message
        );

        // Subscribe to agent responses for this user
        var responseTask = WaitForResponseAsync(userId, ct);

        // Publish message to bus
        await _messageBus.PublishAsync(userMessage, ct);

        // Wait for response (with timeout)
        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2), ct);
        var completedTask = await Task.WhenAny(responseTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            return "Error: Request timed out. Please check if the agent service is running.";
        }

        return await responseTask;
    }

    /// <summary>
    /// Start interactive CLI session
    /// </summary>
    public async Task StartInteractiveAsync(CancellationToken ct = default)
    {
        var userId = _config.UserId ?? "cli-user";
        Console.WriteLine($"CoreBot CLI Interface (User: {userId})");
        Console.WriteLine("Type 'exit' or 'quit' to exit");
        Console.WriteLine();

        while (!ct.IsCancellationRequested)
        {
            Console.Write("You: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            try
            {
                Console.Write("CoreBot: ");
                var response = await SendMessageAsync(input, ct);
                Console.WriteLine(response);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
            }
        }
    }

    private async Task<string> WaitForResponseAsync(string userId, CancellationToken ct)
    {
        await foreach (var message in _messageBus.SubscribeAsync<AgentResponse>(ct))
        {
            if (message.Platform == "cli" && message.UserId == userId)
            {
                return message.Content;
            }
        }

        return "No response received";
    }
}

/// <summary>
/// Configuration for CLI platform
/// </summary>
public class CliConfiguration
{
    public string UserId { get; set; } = "cli-user";
}
