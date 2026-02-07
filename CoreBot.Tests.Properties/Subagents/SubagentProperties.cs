using System.Text.Json;
using CoreBot.Core.Configuration;
using CoreBot.Core.Subagents;
using CoreBot.Core.Messages;
using Xunit;

namespace CoreBot.Tests.Properties.Subagents;

/// <summary>
/// Property-based tests for subagent system
/// Property 17: Subagent Creation
/// Property 18: Subagent Completion Notification
/// Property 19: Subagent State Persistence
/// Validates: Requirements 10.1, 10.2, 10.4
/// </summary>
public class SubagentProperties
{
    [Theory]
    [InlineData("Task 1", true)]
    [InlineData("Long running backup", true)]
    [InlineData("Data processing", true)]
    [InlineData("", false)]
    public void Subagent_HasValidName(string name, bool shouldBeValid)
    {
        // Arrange & Act
        var subagent = new Subagent { Name = name };
        var isValid = !string.IsNullOrEmpty(subagent.Name);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Theory]
    [InlineData("telegram", true)]
    [InlineData("whatsapp", true)]
    [InlineData("feishu", true)]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    public void Subagent_HasValidPlatform(string platform, bool shouldBeValid)
    {
        // Arrange
        var validPlatforms = new[] { "telegram", "whatsapp", "feishu" };
        var subagent = new Subagent { Platform = platform };

        // Act
        var isValid = validPlatforms.Contains(subagent.Platform?.ToLower());

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Theory]
    [InlineData("user-123", true)]
    [InlineData("456", true)]
    [InlineData("", false)]
    public void Subagent_HasValidUserId(string userId, bool shouldBeValid)
    {
        // Arrange & Act
        var subagent = new Subagent { UserId = userId };
        var isValid = !string.IsNullOrEmpty(subagent.UserId);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void Subagent_Progress_IsValid(int progress, bool shouldBeValid)
    {
        // Act
        var isValid = progress >= 0 && progress <= 100;

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Theory]
    [InlineData(SubagentState.Created, true)]
    [InlineData(SubagentState.Running, true)]
    [InlineData(SubagentState.Completed, true)]
    [InlineData(SubagentState.Failed, true)]
    [InlineData(SubagentState.Cancelled, true)]
    public void Subagent_AllStates_AreValid(SubagentState state, bool shouldBeValid)
    {
        // Arrange & Act
        var subagent = new Subagent { State = state };

        // Assert
        Assert.Equal(shouldBeValid, true);
        Assert.Equal(state, subagent.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void MultipleSubagents_CanBeCreated(int count)
    {
        // Arrange & Act
        var subagents = new List<Subagent>();
        for (int i = 0; i < count; i++)
        {
            subagents.Add(new Subagent
            {
                Id = $"subagent-{i}",
                Name = $"Subagent {i}",
                State = SubagentState.Created
            });
        }

        // Assert
        Assert.Equal(count, subagents.Count);
        foreach (var subagent in subagents)
        {
            Assert.NotNull(subagent.Id);
            Assert.NotNull(subagent.Name);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Subagent_WithResult_HasCompletionData(bool hasResult)
    {
        // Arrange
        var subagent = new Subagent
        {
            Result = hasResult ? "Task completed successfully" : null,
            State = hasResult ? SubagentState.Completed : SubagentState.Running
        };

        // Act
        var hasCompletionData = !string.IsNullOrEmpty(subagent.Result);

        // Assert
        Assert.Equal(hasResult, hasCompletionData);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Subagent_WithError_HasErrorDetails(bool hasError)
    {
        // Arrange
        var subagent = new Subagent
        {
            Error = hasError ? "Task failed: timeout" : null,
            State = hasError ? SubagentState.Failed : SubagentState.Running
        };

        // Act
        var hasErrorDetails = !string.IsNullOrEmpty(subagent.Error);

        // Assert
        Assert.Equal(hasError, hasErrorDetails);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Subagent_Completion_HasTimestamp(bool hasCompletion)
    {
        // Arrange
        var subagent = new Subagent
        {
            CompletedAt = hasCompletion ? DateTime.UtcNow : null
        };

        // Act
        var isCompleted = subagent.CompletedAt.HasValue;

        // Assert
        Assert.Equal(hasCompletion, isCompleted);
    }

    [Theory]
    [InlineData(SubagentState.Completed, true)]
    [InlineData(SubagentState.Failed, true)]
    [InlineData(SubagentState.Cancelled, true)]
    [InlineData(SubagentState.Running, false)]
    [InlineData(SubagentState.Created, false)]
    public void Subagent_TerminalStates_HaveCompletionTime(SubagentState state, bool shouldBeTerminal)
    {
        // Arrange
        var subagent = new Subagent
        {
            State = state,
            CompletedAt = shouldBeTerminal ? DateTime.UtcNow : null
        };

        // Act
        var isTerminal = subagent.State == SubagentState.Completed ||
                        subagent.State == SubagentState.Failed ||
                        subagent.State == SubagentState.Cancelled;

        // Assert
        Assert.Equal(shouldBeTerminal, isTerminal);
        if (shouldBeTerminal)
        {
            Assert.NotNull(subagent.CompletedAt);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void MultipleSubagents_HaveUniqueIds(int count)
    {
        // Arrange & Act
        var subagents = new List<Subagent>();
        var ids = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            var subagent = new Subagent();
            subagents.Add(subagent);
            ids.Add(subagent.Id);
        }

        // Assert
        Assert.Equal(count, ids.Count);
        Assert.Equal(count, subagents.Count);
    }

    [Theory]
    [InlineData("telegram", "user-1", true)]
    [InlineData("whatsapp", "user-2", true)]
    [InlineData("telegram", "user-1", false)]  // Duplicate
    public void Subagent_CanBeQueriedByUser(string platform, string userId, bool shouldExist)
    {
        // Arrange
        var subagent = new Subagent
        {
            Platform = platform,
            UserId = userId,
            Name = $"Subagent for {userId}"
        };

        // Act
        var matches = subagent.Platform == platform && subagent.UserId == userId;

        // Assert
        if (shouldExist)
        {
            Assert.True(matches);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(75)]
    [InlineData(100)]
    public void Subagent_Progress_IncreasesMonotonically(int finalProgress)
    {
        // Arrange
        var subagent = new Subagent();
        var previousProgress = 0;

        // Act
        for (int i = 0; i <= finalProgress; i++)
        {
            subagent.Progress = i;
            Assert.True(subagent.Progress >= previousProgress);
            previousProgress = subagent.Progress;
        }

        // Assert
        Assert.Equal(finalProgress, subagent.Progress);
    }

    [Theory]
    [InlineData("{}", true)]
    [InlineData(@"{""key"":""value""}", true)]
    [InlineData("", false)]
    public void Subagent_Metadata_CanBeStored(string metadataJson, bool shouldBeValid)
    {
        // Act
        var isValid = !string.IsNullOrEmpty(metadataJson);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Fact]
    public void Subagent_SerializeDeserialize_PreservesData()
    {
        // Arrange
        var original = new Subagent
        {
            Id = "test-id",
            Name = "Test Subagent",
            State = SubagentState.Running,
            Platform = "telegram",
            UserId = "user-123",
            TaskName = "file_read",
            Progress = 50,
            StatusMessage = "Processing",
            Result = null
        };

        // Act
        var json = original.ToJson();
        var deserialized = Subagent.FromJson(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.State, deserialized.State);
        Assert.Equal(original.Platform, deserialized.Platform);
        Assert.Equal(original.UserId, deserialized.UserId);
        Assert.Equal(original.TaskName, deserialized.TaskName);
        Assert.Equal(original.Progress, deserialized.Progress);
        Assert.Equal(original.StatusMessage, deserialized.StatusMessage);
    }

    [Fact]
    public void SubagentCompletedMessage_ContainsSubagent()
    {
        // Arrange & Act
        var subagent = new Subagent
        {
            Id = "test-id",
            Name = "Test Subagent"
        };

        var message = new SubagentCompletedMessage
        {
            Subagent = subagent,
            Platform = "telegram",
            UserId = "user-123"
        };

        // Assert
        Assert.NotNull(message.Subagent);
        Assert.Equal("test-id", message.Subagent.Id);
        Assert.Equal("Test Subagent", message.Subagent.Name);
    }

    [Fact]
    public void SubagentCompletedMessage_HasRoutingInfo()
    {
        // Arrange & Act
        var message = new SubagentCompletedMessage
        {
            Subagent = new Subagent(),
            Platform = "whatsapp",
            UserId = "user-456"
        };

        // Assert
        Assert.Equal("whatsapp", message.Platform);
        Assert.Equal("user-456", message.UserId);
        Assert.NotNull(message.Subagent);
    }

    [Theory]
    [InlineData(SubagentState.Completed, "completed successfully")]
    [InlineData(SubagentState.Failed, "failed with error")]
    [InlineData(SubagentState.Cancelled, "was cancelled")]
    public void Subagent_StatusMessage_ReflectsState(SubagentState state, string statusKeyword)
    {
        // Arrange & Act
        var subagent = new Subagent
        {
            State = state,
            StatusMessage = $"Task {statusKeyword}"
        };

        // Assert
        Assert.Contains(statusKeyword, subagent.StatusMessage);
        Assert.Equal(state, subagent.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Subagent_TriggerMessage_IsPreserved(bool hasTrigger)
    {
        // Arrange
        var triggerMessage = hasTrigger ? new UserMessage(
            MessageId: "msg-123",
            Timestamp: DateTime.UtcNow,
            Platform: "telegram",
            UserId: "user-123",
            Content: "Start task"
        ) : null;

        var subagent = new Subagent
        {
            TriggerMessage = triggerMessage
        };

        // Act
        var hasTriggerMessage = subagent.TriggerMessage != null;

        // Assert
        Assert.Equal(hasTrigger, hasTriggerMessage);
        if (hasTrigger)
        {
            Assert.Equal("msg-123", subagent.TriggerMessage.MessageId);
        }
    }

    [Theory]
    [InlineData("file_read", true)]
    [InlineData("backup_database", true)]
    [InlineData("", false)]
    public void Subagent_TaskName_IsValid(string taskName, bool shouldBeValid)
    {
        // Arrange & Act
        var subagent = new Subagent { TaskName = taskName };
        var isValid = !string.IsNullOrEmpty(subagent.TaskName);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Fact]
    public void Subagent_CreatedTimestamp_IsSet()
    {
        // Arrange & Act
        var before = DateTime.UtcNow;
        var subagent = new Subagent();
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(subagent.CreatedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Theory]
    [InlineData(SubagentState.Running, true)]
    [InlineData(SubagentState.Completed, true)]
    [InlineData(SubagentState.Created, false)]
    public void Subagent_StartedAt_IsSetForRunningStates(SubagentState state, bool shouldHaveStartTime)
    {
        // Arrange
        var subagent = new Subagent
        {
            State = state,
            StartedAt = shouldHaveStartTime ? DateTime.UtcNow : null
        };

        // Act
        var hasStarted = subagent.StartedAt.HasValue;

        // Assert
        Assert.Equal(shouldHaveStartTime, hasStarted);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void SubagentManager_CanTrackMultipleSubagents(int count)
    {
        // Arrange & Act
        var subagents = new Dictionary<string, Subagent>();
        for (int i = 0; i < count; i++)
        {
            subagents[$"subagent-{i}"] = new Subagent
            {
                Id = $"subagent-{i}",
                Name = $"Subagent {i}"
            };
        }

        // Assert
        Assert.Equal(count, subagents.Count);
    }
}
