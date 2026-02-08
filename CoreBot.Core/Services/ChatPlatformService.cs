using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CoreBot.Core.ChatPlatforms;
using CoreBot.Core.Configuration;

namespace CoreBot.Core.Services;

/// <summary>
/// Hosted service that manages chat platform adapters
/// </summary>
public class ChatPlatformService : BackgroundService
{
    private readonly ILogger<ChatPlatformService> _logger;
    private readonly CoreBotConfiguration _config;
    private readonly IEnumerable<IChatPlatform> _platforms;

    public ChatPlatformService(
        ILogger<ChatPlatformService> logger,
        IOptions<CoreBotConfiguration> config,
        IEnumerable<IChatPlatform> platforms)
    {
        _logger = logger;
        _config = config.Value;
        _platforms = platforms;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChatPlatformService starting...");

        // Connect to each enabled platform
        var connectTasks = _platforms.Select(platform => ConnectAndStartPlatformAsync(platform, stoppingToken));
        await Task.WhenAll(connectTasks);

        _logger.LogInformation("ChatPlatformService started. Connected to {Count} platform(s).", _platforms.Count());

        // Keep running until cancelled
        await Task.Delay(Timeout.Infinite, stoppingToken);

        // Disconnect on shutdown
        _logger.LogInformation("ChatPlatformService stopping...");
        var disconnectTasks = _platforms.Select(platform => platform.DisconnectAsync(stoppingToken));
        await Task.WhenAll(disconnectTasks);
        _logger.LogInformation("ChatPlatformService stopped.");
    }

    private async Task ConnectAndStartPlatformAsync(IChatPlatform platform, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Connecting to platform: {Platform}...", platform.PlatformName);
            await platform.ConnectAsync(ct);
            _logger.LogInformation("Starting to receive messages from {Platform}...", platform.PlatformName);
            _ = platform.StartReceivingAsync(ct); // Start in background
            _logger.LogInformation("Successfully connected to {Platform}", platform.PlatformName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to platform: {Platform}", platform.PlatformName);
        }
    }
}
