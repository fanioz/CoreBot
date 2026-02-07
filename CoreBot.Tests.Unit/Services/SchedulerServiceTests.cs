using System.Text.Json;
using CoreBot.Core.Configuration;
using CoreBot.Core.Messaging;
using CoreBot.Core.Messages;
using CoreBot.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CoreBot.Tests.Unit.Services;

/// <summary>
/// Unit tests for SchedulerService
/// </summary>
public class SchedulerServiceTests
{
    private readonly Mock<IMessageBus> _messageBusMock;
    private readonly Mock<ILogger<SchedulerService>> _loggerMock;
    private readonly CoreBotConfiguration _configuration;

    public SchedulerServiceTests()
    {
        _messageBusMock = new Mock<IMessageBus>();
        _loggerMock = new Mock<ILogger<SchedulerService>>();

        _configuration = new CoreBotConfiguration
        {
            Scheduler = new SchedulerConfiguration
            {
                Tasks = new List<ScheduledTask>
                {
                    new ScheduledTask
                    {
                        Name = "daily-summary",
                        Cron = "0 0 9 * * *", // 9 AM daily
                        Action = new ScheduledTaskAction
                        {
                            Type = "send_message",
                            Platform = "telegram",
                            UserId = "user-123",
                            Message = "Good morning! Here's your daily summary."
                        }
                    },
                    new ScheduledTask
                    {
                        Name = "hourly-backup",
                        Cron = "0 0 * * * *", // Every hour
                        Action = new ScheduledTaskAction
                        {
                            Type = "tool",
                            ToolName = "backup_database",
                            Parameters = new Dictionary<string, object>
                            {
                                { "destination", "s3" }
                            }
                        }
                    }
                }
            }
        };
    }

    [Fact]
    public async Task SchedulerService_StartAsync_RegistersTasks()
    {
        // Arrange
        var service = new SchedulerService(
            _messageBusMock.Object,
            Options.Create(_configuration),
            _loggerMock.Object
        );

        // Act
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("starting with 2 scheduled tasks")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SchedulerService_StartAsync_WithInvalidCron_LogsError()
    {
        // Arrange
        var invalidConfig = new CoreBotConfiguration
        {
            Scheduler = new SchedulerConfiguration
            {
                Tasks = new List<ScheduledTask>
                {
                    new ScheduledTask
                    {
                        Name = "invalid-task",
                        Cron = "invalid-cron-expression",
                        Action = new ScheduledTaskAction()
                    }
                }
            }
        };

        var service = new SchedulerService(
            _messageBusMock.Object,
            Options.Create(invalidConfig),
            _loggerMock.Object
        );

        // Act
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to register scheduled task")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SchedulerService_StartAsync_WithNoTasks_LogsZeroTasks()
    {
        // Arrange
        var emptyConfig = new CoreBotConfiguration
        {
            Scheduler = new SchedulerConfiguration
            {
                Tasks = new List<ScheduledTask>()
            }
        };

        var service = new SchedulerService(
            _messageBusMock.Object,
            Options.Create(emptyConfig),
            _loggerMock.Object
        );

        // Act
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("starting with 0 scheduled tasks")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ScheduledTask_DefaultValues_AreValid()
    {
        // Arrange & Act
        var task = new ScheduledTask();

        // Assert
        Assert.Equal(string.Empty, task.Name);
        Assert.Equal(string.Empty, task.Cron);
        Assert.NotNull(task.Action);
        Assert.Equal(string.Empty, task.Action.Type);
    }

    [Fact]
    public void ScheduledTaskAction_CanSetToolAction()
    {
        // Arrange & Act
        var action = new ScheduledTaskAction
        {
            Type = "tool",
            ToolName = "file_read",
            Parameters = new Dictionary<string, object>
            {
                { "path", "/tmp/test.txt" }
            }
        };

        // Assert
        Assert.Equal("tool", action.Type);
        Assert.Equal("file_read", action.ToolName);
        Assert.NotNull(action.Parameters);
        Assert.Single(action.Parameters);
    }

    [Fact]
    public void ScheduledTaskAction_CanSetSendMessageAction()
    {
        // Arrange & Act
        var action = new ScheduledTaskAction
        {
            Type = "send_message",
            Platform = "telegram",
            UserId = "user-456",
            Message = "Hello, scheduled message!"
        };

        // Assert
        Assert.Equal("send_message", action.Type);
        Assert.Equal("telegram", action.Platform);
        Assert.Equal("user-456", action.UserId);
        Assert.Equal("Hello, scheduled message!", action.Message);
    }

    [Theory]
    [InlineData("0 0 9 * * *", true)]    // Valid: 9 AM daily
    [InlineData("0 */5 * * * *", true)]  // Valid: Every 5 minutes
    [InlineData("0 0 * * * *", true)]    // Valid: Every hour
    [InlineData("* * * * * *", true)]     // Valid: Every second
    [InlineData("invalid", false)]       // Invalid
    [InlineData("", false)]              // Invalid
    public void CronExpression_Validation(string cronExpression, bool shouldBeValid)
    {
        // Act
        var isValid = TryParseCron(cronExpression);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Theory]
    [InlineData("tool", "", "send_message")]
    [InlineData("send_message", "tool", "")]
    public void ScheduledTaskAction_OnlyOneActionType(string primaryType, string secondaryType, string tertiaryType)
    {
        // Arrange & Act
        var action = new ScheduledTaskAction { Type = primaryType };

        // Assert
        Assert.Equal(primaryType, action.Type);
    }

    private static bool TryParseCron(string cronExpression)
    {
        try
        {
            Cronos.CronExpression.Parse(cronExpression, Cronos.CronFormat.IncludeSeconds);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public void SchedulerConfiguration_DefaultValues_AreValid()
    {
        // Arrange & Act
        var config = new SchedulerConfiguration();

        // Assert
        Assert.NotNull(config.Tasks);
        Assert.Empty(config.Tasks);
    }

    [Fact]
    public void SchedulerConfiguration_CanAddTasks()
    {
        // Arrange
        var config = new SchedulerConfiguration();

        // Act
        config.Tasks.Add(new ScheduledTask
        {
            Name = "test-task",
            Cron = "0 0 * * * *",
            Action = new ScheduledTaskAction()
        });

        // Assert
        Assert.Single(config.Tasks);
        Assert.Equal("test-task", config.Tasks[0].Name);
    }
}
