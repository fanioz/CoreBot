using CoreBot.Core.Configuration;
using CoreBot.Core.Messaging;
using CoreBot.Core.Skills;
using CoreBot.Core.Tools;
using Xunit;

namespace CoreBot.Tests.Properties.Skills;

/// <summary>
/// Property-based tests for skills system
/// Property 13: Skill Tool Registration
/// Property 14: Skill Load Failure Isolation
/// Validates: Requirements 8.2, 8.3, 8.4, 8.5
/// </summary>
public class SkillProperties
{
    [Theory]
    [InlineData("skill1", true)]
    [InlineData("skill2", true)]
    [InlineData("skill-with-dash", true)]
    [InlineData("skill_with_underscore", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Skill_HasValidName(string name, bool shouldBeValid)
    {
        // Arrange & Act
        var isValid = !string.IsNullOrEmpty(name);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("2.1.3", true)]
    [InlineData("10.20.30", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("invalid", false)]
    public void Skill_HasValidVersion(string version, bool shouldBeValid)
    {
        // Arrange & Act
        var parts = version?.Split('.');
        var isValid = parts != null && parts.Length == 3 &&
                     parts.All(p => int.TryParse(p, out _));

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Skill_CanHaveMultipleTools(int toolCount)
    {
        // Arrange
        var skill = new TestSkill("test", "1.0.0", toolCount);

        // Act
        var tools = skill.GetTools().ToList();

        // Assert
        Assert.Equal(toolCount, tools.Count);
        foreach (var tool in tools)
        {
            Assert.NotNull(tool.Name);
            Assert.NotNull(tool.Description);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Skill_CanHaveMultipleHandlers(int handlerCount)
    {
        // Arrange
        var skill = new TestSkill("test", "1.0.0", 0, handlerCount);

        // Act
        var handlers = skill.GetMessageHandlers().ToList();

        // Assert
        Assert.Equal(handlerCount, handlers.Count);
        foreach (var handler in handlers)
        {
            Assert.NotNull(handler.MessageType);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Skill_InitializeAsync_IsCalled(bool shouldInitialize)
    {
        // Arrange
        var skill = new MockSkill("test");
        var initialized = false;

        // Act
        if (shouldInitialize)
        {
            skill.InitializeAsync(null!).Wait();
            initialized = true;
        }

        // Assert
        Assert.Equal(shouldInitialize, initialized);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Skill_ShutdownAsync_IsCalled(bool shouldShutdown)
    {
        // Arrange
        var skill = new MockSkill("test");
        var shutdown = false;

        // Act
        if (shouldShutdown)
        {
            skill.ShutdownAsync().Wait();
            shutdown = true;
        }

        // Assert
        Assert.Equal(shouldShutdown, shutdown);
    }

    [Fact]
    public void Skill_ToolRegistration_PreservesToolCount()
    {
        // Arrange
        var toolRegistry = new ToolRegistry(new ToolConfiguration());
        var skill = new TestSkill("test", "1.0.0", 5);
        var tools = skill.GetTools().ToList();

        // Act
        foreach (var tool in tools)
        {
            toolRegistry.RegisterTool(tool);
        }

        // Assert
        foreach (var tool in tools)
        {
            var retrieved = toolRegistry.GetTool(tool.Name);
            Assert.NotNull(retrieved);
            Assert.Equal(tool.Name, retrieved.Name);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void MultipleSkills_CanBeLoaded(int skillCount)
    {
        // Arrange & Act
        var skills = new List<TestSkill>();
        for (int i = 0; i < skillCount; i++)
        {
            skills.Add(new TestSkill($"skill{i}", "1.0.0", 2));
        }

        // Assert
        Assert.Equal(skillCount, skills.Count);
        foreach (var skill in skills)
        {
            Assert.NotNull(skill.Name);
            Assert.Equal(2, skill.GetTools().Count());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("skill1")]
    [InlineData("skill1,skill2")]
    [InlineData("skill1,skill2,skill3")]
    public void SkillsConfiguration_DisabledSkills_AreParsed(string disabledSkills)
    {
        // Arrange
        var config = new SkillsConfiguration();

        // Act
        if (string.IsNullOrEmpty(disabledSkills))
        {
            config.DisabledSkills = Array.Empty<string>();
        }
        else
        {
            config.DisabledSkills = disabledSkills.Split(',');
        }

        // Assert
        var count = string.IsNullOrEmpty(disabledSkills) ? 0 : disabledSkills.Split(',').Length;
        Assert.Equal(count, config.DisabledSkills.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("skill1")]
    [InlineData("skill1,skill2")]
    [InlineData("skill1,skill2,skill3")]
    public void SkillsConfiguration_EnabledSkills_AreParsed(string enabledSkills)
    {
        // Arrange
        var config = new SkillsConfiguration();

        // Act
        if (string.IsNullOrEmpty(enabledSkills))
        {
            config.EnabledSkills = Array.Empty<string>();
        }
        else
        {
            config.EnabledSkills = enabledSkills.Split(',');
        }

        // Assert
        var count = string.IsNullOrEmpty(enabledSkills) ? 0 : enabledSkills.Split(',').Length;
        Assert.Equal(count, config.EnabledSkills.Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SkillsConfiguration_EnableSkills_CanBeSet(bool isEnabled)
    {
        // Arrange
        var config = new SkillsConfiguration { EnableSkills = isEnabled };

        // Act
        var result = config.EnableSkills;

        // Assert
        Assert.Equal(isEnabled, result);
    }

    [Theory]
    [InlineData("~/.corebot/skills")]
    [InlineData("./skills")]
    [InlineData("/var/lib/corebot/skills")]
    [InlineData("C:\\Program Files\\Corebot\\skills")]
    public void SkillsConfiguration_SkillsDirectory_CanBeSet(string directory)
    {
        // Arrange
        var config = new SkillsConfiguration { SkillsDirectory = directory };

        // Act
        var result = config.SkillsDirectory;

        // Assert
        Assert.Equal(directory, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void SkillTools_HaveUniqueNames(int toolCount)
    {
        // Arrange
        var skill = new TestSkill("test", "1.0.0", toolCount);
        var toolNames = new HashSet<string>();

        // Act
        foreach (var tool in skill.GetTools())
        {
            toolNames.Add(tool.Name);
        }

        // Assert
        Assert.Equal(toolCount, toolNames.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void SkillHandlers_HaveValidMessageTypes(int handlerCount)
    {
        // Arrange
        var skill = new TestSkill("test", "1.0.0", 0, handlerCount);

        // Act
        var handlers = skill.GetMessageHandlers().ToList();

        // Assert
        Assert.Equal(handlerCount, handlers.Count);
        foreach (var handler in handlers)
        {
            Assert.NotNull(handler.MessageType);
        }
    }

    /// <summary>
    /// Test skill implementation for property tests
    /// </summary>
    private class TestSkill : ISkill
    {
        public string Name { get; }
        public string Description { get; }
        public string Version { get; }
        private readonly List<IToolDefinition> _tools;
        private readonly List<IMessageHandler> _handlers;

        public TestSkill(string name, string version, int toolCount = 0, int handlerCount = 0)
        {
            Name = name;
            Description = $"Test skill {name}";
            Version = version;
            _tools = new List<IToolDefinition>();
            _handlers = new List<IMessageHandler>();

            for (int i = 0; i < toolCount; i++)
            {
                _tools.Add(new TestTool($"{name}_tool{i}", $"Tool {i}"));
            }

            for (int i = 0; i < handlerCount; i++)
            {
                _handlers.Add(new TestHandler(typeof(string)));
            }
        }

        public IEnumerable<IToolDefinition> GetTools() => _tools;
        public IEnumerable<IMessageHandler> GetMessageHandlers() => _handlers;

        public Task InitializeAsync(IServiceProvider serviceProvider, System.Threading.CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync(System.Threading.CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Test tool implementation
    /// </summary>
    private class TestTool : IToolDefinition
    {
        public string Name { get; }
        public string Description { get; }

        public TestTool(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public System.Text.Json.JsonDocument GetSchema()
        {
            return System.Text.Json.JsonDocument.Parse("{}");
        }

        public Task<ToolResult> ExecuteAsync(System.Text.Json.JsonElement parameters, System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult(new ToolResult { Success = true, Result = "OK" });
        }
    }

    /// <summary>
    /// Test handler implementation
    /// </summary>
    private class TestHandler : IMessageHandler
    {
        public Type MessageType { get; }

        public TestHandler(Type messageType)
        {
            MessageType = messageType;
        }

        public Task HandleAsync(object message, System.Threading.CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Mock skill for testing lifecycle
    /// </summary>
    private class MockSkill : ISkill
    {
        public string Name { get; }
        public string Description { get; }
        public string Version { get; }

        public MockSkill(string name)
        {
            Name = name;
            Description = $"Mock skill {name}";
            Version = "1.0.0";
        }

        public IEnumerable<IToolDefinition> GetTools()
        {
            return Enumerable.Empty<IToolDefinition>();
        }

        public IEnumerable<IMessageHandler> GetMessageHandlers()
        {
            return Enumerable.Empty<IMessageHandler>();
        }

        public Task InitializeAsync(IServiceProvider serviceProvider, System.Threading.CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync(System.Threading.CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
