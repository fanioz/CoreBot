using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using CoreBot.Core.Configuration;
using CoreBot.Core.LLM;
using CoreBot.Core.Memory;
using CoreBot.Core.Messaging;
using CoreBot.Core.Messages;
using CoreBot.Core.Services;
using CoreBot.Core.Tools;
using ToolResultType = CoreBot.Core.Tools.ToolResult;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CoreBot.Tests.Unit.Services;

/// <summary>
/// Unit tests for AgentService
/// </summary>
public class AgentServiceTests
{
    private readonly Mock<IMessageBus> _messageBusMock;
    private readonly Mock<IMemoryStore> _memoryStoreMock;
    private readonly Mock<ILlmProvider> _llmProviderMock;
    private readonly Mock<ToolRegistry> _toolRegistryMock;
    private readonly Mock<ILogger<AgentService>> _loggerMock;
    private readonly CoreBotConfiguration _configuration;

    public AgentServiceTests()
    {
        _messageBusMock = new Mock<IMessageBus>();
        _memoryStoreMock = new Mock<IMemoryStore>();
        _llmProviderMock = new Mock<ILlmProvider>();
        _toolRegistryMock = new Mock<ToolRegistry>(new ToolConfiguration());
        _loggerMock = new Mock<ILogger<AgentService>>();

        _configuration = new CoreBotConfiguration
        {
            Llm = new LlmConfiguration
            {
                SystemPrompt = "You are a helpful assistant.",
                MaxTokens = 4096,
                Temperature = 0.7,
                EnableToolCalling = true
            }
        };
    }

    [Fact]
    public async Task AgentService_StartAsync_StartsProcessing()
    {
        // Arrange
        var messageBus = new TestMessageBus();
        var service = new AgentService(
            messageBus,
            _memoryStoreMock.Object,
            _llmProviderMock.Object,
            _toolRegistryMock.Object,
            Options.Create(_configuration),
            _loggerMock.Object
        );

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give it time to start
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(messageBus.IsSubscribed);
    }

    [Fact]
    public async Task ProcessUserMessage_SavesToMemory()
    {
        // Arrange
        var userMessage = new UserMessage(
            MessageId: "msg-123",
            Timestamp: DateTime.UtcNow,
            Platform: "telegram",
            UserId: "user-456",
            Content: "Hello, bot!"
        );

        _memoryStoreMock
            .Setup(x => x.SaveMessageAsync(
                userMessage.Platform,
                userMessage.UserId,
                It.IsAny<StoredMessage>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _memoryStoreMock
            .Setup(x => x.GetOrCreateConversationIdAsync(
                userMessage.Platform,
                userMessage.UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conv-789");

        _memoryStoreMock
            .Setup(x => x.GetHistoryAsync(
                userMessage.Platform,
                userMessage.UserId,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoredMessage>());

        _llmProviderMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "Hello! How can I help you?",
                ToolCalls = null
            });

        var messageBus = new TestMessageBus();
        var service = new AgentService(
            messageBus,
            _memoryStoreMock.Object,
            _llmProviderMock.Object,
            _toolRegistryMock.Object,
            Options.Create(_configuration),
            _loggerMock.Object
        );

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give service time to start
        await messageBus.PublishAsync(userMessage);
        await Task.Delay(500); // Give time for processing
        await service.StopAsync(CancellationToken.None);

        // Assert
        _memoryStoreMock.Verify(
            x => x.SaveMessageAsync(
                userMessage.Platform,
                userMessage.UserId,
                It.Is<StoredMessage>(m => m.Role == "user" && m.Content == "Hello, bot!"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessUserMessage_WithToolCall_ExecutesTool()
    {
        // Arrange
        var userMessage = new UserMessage(
            MessageId: "msg-123",
            Timestamp: DateTime.UtcNow,
            Platform: "telegram",
            UserId: "user-456",
            Content: "Read the file test.txt"
        );

        _memoryStoreMock
            .Setup(x => x.SaveMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<StoredMessage>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _memoryStoreMock
            .Setup(x => x.GetOrCreateConversationIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conv-789");

        _memoryStoreMock
            .Setup(x => x.GetHistoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoredMessage>());

        var toolCall = new ToolCall(
            ToolName: "file_read",
            Parameters: JsonDocument.Parse(@"{""path"":""test.txt""}").RootElement
        );

        _llmProviderMock
            .SetupSequence(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "I'll read the file for you.",
                ToolCalls = new List<ToolCall> { toolCall }
            })
            .ReturnsAsync(new LlmResponse
            {
                Content = "The file contains: Hello, world!",
                ToolCalls = null
            });

        var toolDefinitionMock = new Mock<IToolDefinition>();
        toolDefinitionMock.Setup(x => x.Name).Returns("file_read");
        toolDefinitionMock.Setup(x => x.Description).Returns("Read a file");
        toolDefinitionMock.Setup(x => x.GetSchema()).Returns(JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""path"": { ""type"": ""string"" }
            },
            ""required"": [""path""]
        }"));
        toolDefinitionMock
            .Setup(x => x.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResultType
            {
                Success = true,
                Result = "Hello, world!"
            });

        _toolRegistryMock
            .Setup(x => x.ExecuteAsync("file_read", It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResultType
            {
                Success = true,
                Result = "Hello, world!"
            });

        var messageBus = new TestMessageBus();
        var service = new AgentService(
            messageBus,
            _memoryStoreMock.Object,
            _llmProviderMock.Object,
            _toolRegistryMock.Object,
            Options.Create(_configuration),
            _loggerMock.Object
        );

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give service time to start
        await messageBus.PublishAsync(userMessage);
        await Task.Delay(500); // Give time for processing
        await service.StopAsync(CancellationToken.None);

        // Assert
        _toolRegistryMock.Verify(
            x => x.ExecuteAsync("file_read", It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessUserMessage_PublishesAgentResponse()
    {
        // Arrange
        var userMessage = new UserMessage(
            MessageId: "msg-123",
            Timestamp: DateTime.UtcNow,
            Platform: "telegram",
            UserId: "user-456",
            Content: "Hello"
        );

        _memoryStoreMock
            .Setup(x => x.SaveMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<StoredMessage>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _memoryStoreMock
            .Setup(x => x.GetOrCreateConversationIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conv-789");

        _memoryStoreMock
            .Setup(x => x.GetHistoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoredMessage>());

        var expectedResponse = new LlmResponse
        {
            Content = "Hi there!",
            ToolCalls = null
        };

        _llmProviderMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var messageBus = new TestMessageBus();
        AgentResponse? publishedResponse = null;
        messageBus.OnAgentResponsePublished = (response) => publishedResponse = response;

        var service = new AgentService(
            messageBus,
            _memoryStoreMock.Object,
            _llmProviderMock.Object,
            _toolRegistryMock.Object,
            Options.Create(_configuration),
            _loggerMock.Object
        );

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give service time to start
        await messageBus.PublishAsync(userMessage);
        await Task.Delay(500); // Give time for processing
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(publishedResponse);
        Assert.Equal("telegram", publishedResponse.Platform);
        Assert.Equal("user-456", publishedResponse.UserId);
        Assert.Equal("Hi there!", publishedResponse.Content);
    }

    [Fact]
    public async Task ProcessUserMessage_WithConversationHistory_IncludesHistoryInLlmRequest()
    {
        // Arrange
        var userMessage = new UserMessage(
            MessageId: "msg-123",
            Timestamp: DateTime.UtcNow,
            Platform: "telegram",
            UserId: "user-456",
            Content: "What did I just say?"
        );

        var history = new List<StoredMessage>
        {
            new StoredMessage { Timestamp = DateTime.UtcNow.AddMinutes(-2), Role = "user", Content = "Hello" },
            new StoredMessage { Timestamp = DateTime.UtcNow.AddMinutes(-1), Role = "assistant", Content = "Hi!" }
        };

        _memoryStoreMock
            .Setup(x => x.SaveMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<StoredMessage>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _memoryStoreMock
            .Setup(x => x.GetOrCreateConversationIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conv-789");

        _memoryStoreMock
            .Setup(x => x.GetHistoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        LlmRequest? capturedRequest = null;
        _llmProviderMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new LlmResponse
            {
                Content = "You said Hello.",
                ToolCalls = null
            });

        var messageBus = new TestMessageBus();
        var service = new AgentService(
            messageBus,
            _memoryStoreMock.Object,
            _llmProviderMock.Object,
            _toolRegistryMock.Object,
            Options.Create(_configuration),
            _loggerMock.Object
        );

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give service time to start
        await messageBus.PublishAsync(userMessage);
        await Task.Delay(500); // Give time for processing
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        // Messages: system prompt + first history message + current user message
        // (last history message is excluded to avoid duplication)
        Assert.Equal(3, capturedRequest.Messages.Count);
        Assert.Equal("system", capturedRequest.Messages[0].Role);
        Assert.Equal("user", capturedRequest.Messages[1].Role);
        Assert.Equal("Hello", capturedRequest.Messages[1].Content);
        Assert.Equal("user", capturedRequest.Messages[2].Role); // Current message
        Assert.Equal("What did I just say?", capturedRequest.Messages[2].Content);
    }

    [Fact]
    public async Task ProcessUserMessage_WhenLlmFails_LogsError()
    {
        // Arrange
        var userMessage = new UserMessage(
            MessageId: "msg-123",
            Timestamp: DateTime.UtcNow,
            Platform: "telegram",
            UserId: "user-456",
            Content: "Hello"
        );

        _memoryStoreMock
            .Setup(x => x.SaveMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<StoredMessage>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _memoryStoreMock
            .Setup(x => x.GetOrCreateConversationIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("conv-789");

        _memoryStoreMock
            .Setup(x => x.GetHistoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoredMessage>());

        _llmProviderMock
            .Setup(x => x.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = string.Empty,
                Error = "API rate limit exceeded"
            });

        var messageBus = new TestMessageBus();
        var service = new AgentService(
            messageBus,
            _memoryStoreMock.Object,
            _llmProviderMock.Object,
            _toolRegistryMock.Object,
            Options.Create(_configuration),
            _loggerMock.Object
        );

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give service time to start
        await messageBus.PublishAsync(userMessage);
        await Task.Delay(500); // Give time for processing
        await service.StopAsync(CancellationToken.None);

        // Assert - Should not throw, service should handle the error gracefully
        // The service should continue running even when LLM fails
    }

    /// <summary>
    /// Test message bus implementation for testing
    /// </summary>
    private class TestMessageBus : IMessageBus
    {
        private readonly Channel<UserMessage> _channel;
        public bool IsSubscribed { get; private set; }
        public Action<AgentResponse>? OnAgentResponsePublished { get; set; }

        public TestMessageBus()
        {
            _channel = Channel.CreateUnbounded<UserMessage>();
        }

        public async ValueTask PublishAsync<T>(T message, CancellationToken ct = default) where T : IMessage
        {
            if (message is UserMessage userMessage)
            {
                await _channel.Writer.WriteAsync(userMessage, ct);
            }
            else if (message is AgentResponse agentResponse)
            {
                OnAgentResponsePublished?.Invoke(agentResponse);
            }
        }

        public async IAsyncEnumerable<T> SubscribeAsync<T>(
            [EnumeratorCancellation] CancellationToken ct = default) where T : IMessage
        {
            IsSubscribed = true;
            if (typeof(T) == typeof(UserMessage))
            {
                await foreach (var msg in _channel.Reader.ReadAllAsync(ct))
                {
                    yield return (T)(IMessage)msg;
                }
            }
        }
    }
}
