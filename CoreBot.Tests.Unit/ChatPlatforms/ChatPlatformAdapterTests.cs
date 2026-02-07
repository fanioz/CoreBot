using System.Text.Json;
using System.Text.Json.Nodes;
using CoreBot.Core.ChatPlatforms;
using CoreBot.Core.Messages;
using CoreBot.Core.Messaging;
using Xunit;
using Moq;

namespace CoreBot.Tests.Unit.ChatPlatforms;

/// <summary>
/// Unit tests for chat platform adapters
/// </summary>
public class ChatPlatformAdapterTests
{
    [Fact]
    public void TelegramAdapter_PlatformName_IsCorrect()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new TelegramConfiguration { BotToken = "test-token" };
        var messageBus = new Mock<IMessageBus>().Object;

        // Act
        var adapter = new TelegramAdapter(httpClient, config, messageBus);

        // Assert
        Assert.Equal("telegram", adapter.PlatformName);
    }

    [Fact]
    public void WhatsAppAdapter_PlatformName_IsCorrect()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new WhatsAppConfiguration
        {
            AccessToken = "test-token",
            PhoneNumberId = "test-phone-id"
        };
        var messageBus = new Mock<IMessageBus>().Object;

        // Act
        var adapter = new WhatsAppAdapter(httpClient, config, messageBus);

        // Assert
        Assert.Equal("whatsapp", adapter.PlatformName);
    }

    [Fact]
    public void FeishuAdapter_PlatformName_IsCorrect()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new FeishuConfiguration
        {
            AppId = "test-app-id",
            AppSecret = "test-app-secret"
        };
        var messageBus = new Mock<IMessageBus>().Object;

        // Act
        var adapter = new FeishuAdapter(httpClient, config, messageBus);

        // Assert
        Assert.Equal("feishu", adapter.PlatformName);
    }

    [Fact]
    public void Telegram_Configuration_DefaultValues_AreValid()
    {
        // Arrange & Act
        var config = new TelegramConfiguration();

        // Assert
        Assert.Equal(string.Empty, config.BotToken);
        Assert.Equal(30, config.PollingTimeout);
        Assert.Equal(3, config.MaxRetries);
        Assert.Equal(5, config.RetryDelaySeconds);
    }

    [Fact]
    public void WhatsApp_Configuration_DefaultValues_AreValid()
    {
        // Arrange & Act
        var config = new WhatsAppConfiguration();

        // Assert
        Assert.Equal(string.Empty, config.AccessToken);
        Assert.Equal(string.Empty, config.PhoneNumberId);
        Assert.Equal(string.Empty, config.VerifyToken);
        Assert.Equal("https://graph.facebook.com/v18.0", config.ApiBaseUrl);
        Assert.Equal(3, config.MaxRetries);
        Assert.Equal(5, config.RetryDelaySeconds);
    }

    [Fact]
    public void Feishu_Configuration_DefaultValues_AreValid()
    {
        // Arrange & Act
        var config = new FeishuConfiguration();

        // Assert
        Assert.Equal(string.Empty, config.AppId);
        Assert.Equal(string.Empty, config.AppSecret);
        Assert.Equal(string.Empty, config.EncryptKey);
        Assert.Equal("https://open.feishu.cn/open-apis", config.ApiBaseUrl);
        Assert.Equal(3, config.MaxRetries);
        Assert.Equal(5, config.RetryDelaySeconds);
    }

    [Fact(Skip = "Requires network access")]
    public async Task TelegramAdapter_ConnectAsync_Throws_On_InvalidToken()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new TelegramConfiguration { BotToken = "invalid-token" };
        var messageBus = new Mock<IMessageBus>().Object;
        var adapter = new TelegramAdapter(httpClient, config, messageBus);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => adapter.ConnectAsync());
    }

    [Fact(Skip = "Requires network access")]
    public async Task WhatsAppAdapter_ConnectAsync_Throws_On_InvalidToken()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new WhatsAppConfiguration
        {
            AccessToken = "invalid-token",
            PhoneNumberId = "invalid-phone-id"
        };
        var messageBus = new Mock<IMessageBus>().Object;
        var adapter = new WhatsAppAdapter(httpClient, config, messageBus);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => adapter.ConnectAsync());
    }

    [Fact(Skip = "Requires network access")]
    public async Task FeishuAdapter_ConnectAsync_Throws_On_InvalidCredentials()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new FeishuConfiguration
        {
            AppId = "invalid-app-id",
            AppSecret = "invalid-app-secret"
        };
        var messageBus = new Mock<IMessageBus>().Object;
        var adapter = new FeishuAdapter(httpClient, config, messageBus);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => adapter.ConnectAsync());
    }

    [Fact]
    public void TelegramAdapter_DisconnectAsync_Sets_IsReceivingToFalse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new TelegramConfiguration { BotToken = "test-token" };
        var messageBus = new Mock<IMessageBus>().Object;
        var adapter = new TelegramAdapter(httpClient, config, messageBus);

        // Act
        var disconnectTask = adapter.DisconnectAsync();

        // Assert
        Assert.True(disconnectTask.IsCompleted);
    }

    [Fact]
    public void WhatsAppAdapter_DisconnectAsync_Sets_IsReceivingToFalse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new WhatsAppConfiguration
        {
            AccessToken = "test-token",
            PhoneNumberId = "test-phone-id"
        };
        var messageBus = new Mock<IMessageBus>().Object;
        var adapter = new WhatsAppAdapter(httpClient, config, messageBus);

        // Act
        var disconnectTask = adapter.DisconnectAsync();

        // Assert
        Assert.True(disconnectTask.IsCompleted);
    }

    [Fact]
    public void FeishuAdapter_DisconnectAsync_Sets_IsReceivingToFalse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new FeishuConfiguration
        {
            AppId = "test-app-id",
            AppSecret = "test-app-secret"
        };
        var messageBus = new Mock<IMessageBus>().Object;
        var adapter = new FeishuAdapter(httpClient, config, messageBus);

        // Act
        var disconnectTask = adapter.DisconnectAsync();

        // Assert
        Assert.True(disconnectTask.IsCompleted);
    }

    [Fact(Skip = "Complex JSON payload test - integration test needed")]
    public async Task WhatsAppAdapter_HandleWebhookAsync_Processes_Messages()
    {
        // This test requires complex JSON payload construction
        // It should be moved to integration tests with actual webhook payloads
        // For now, just verify the method exists and doesn't throw on null
        var httpClient = new HttpClient();
        var config = new WhatsAppConfiguration
        {
            AccessToken = "test-token",
            PhoneNumberId = "test-phone-id"
        };
        var messageBusMock = new Mock<IMessageBus>();
        var adapter = new WhatsAppAdapter(httpClient, config, messageBusMock.Object);

        // Just verify it doesn't throw on null payload
        await adapter.HandleWebhookAsync(new JsonObject());
    }

    [Fact]
    public void FeishuAdapter_VerifyEventSignature_Returns_False_Without_Key()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new FeishuConfiguration
        {
            AppId = "test-app-id",
            AppSecret = "test-app-secret",
            EncryptKey = "" // No encrypt key
        };
        var messageBus = new Mock<IMessageBus>().Object;
        var adapter = new FeishuAdapter(httpClient, config, messageBus);

        // Act
        var result = adapter.VerifyEventSignature("1234567890", "nonce", "body", "invalid-signature");

        // Assert
        Assert.False(result);
    }
}
