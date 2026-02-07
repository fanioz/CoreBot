using System.Reflection;
using System.Text.Json;
using CoreBot.Core.Configuration;
using CoreBot.Core.Messaging;
using CoreBot.Core.Skills;
using CoreBot.Core.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoreBot.Tests.Unit.Skills;

/// <summary>
/// Unit tests for SkillLoader
/// </summary>
public class SkillLoaderTests
{
    private readonly Mock<IMessageBus> _messageBusMock;
    private readonly Mock<ILogger<SkillLoader>> _loggerMock;
    private readonly ToolRegistry _toolRegistry;
    private readonly SkillsConfiguration _configuration;
    private readonly Mock<IServiceProvider> _serviceProviderMock;

    public SkillLoaderTests()
    {
        _messageBusMock = new Mock<IMessageBus>();
        _loggerMock = new Mock<ILogger<SkillLoader>>();
        _toolRegistry = new ToolRegistry(new ToolConfiguration());
        _configuration = new SkillsConfiguration();
        _serviceProviderMock = new Mock<IServiceProvider>();
    }

    [Fact]
    public void SkillLoader_Constructor_InitializesSuccessfully()
    {
        // Act
        var loader = new SkillLoader(
            _configuration,
            _toolRegistry,
            _messageBusMock.Object,
            _loggerMock.Object
        );

        // Assert
        Assert.NotNull(loader);
        Assert.Empty(loader.GetLoadedSkills());
    }

    [Fact]
    public async Task LoadSkillsAsync_WhenDisabled_DoesNotLoad()
    {
        // Arrange
        _configuration.EnableSkills = false;
        var loader = new SkillLoader(
            _configuration,
            _toolRegistry,
            _messageBusMock.Object,
            _loggerMock.Object
        );

        // Act
        await loader.LoadSkillsAsync(_serviceProviderMock.Object);

        // Assert
        Assert.Empty(loader.GetLoadedSkills());
    }

    [Fact]
    public async Task LoadSkillsAsync_WhenDirectoryDoesNotExist_ReturnsEmpty()
    {
        // Arrange
        _configuration.SkillsDirectory = "/nonexistent/directory";
        var loader = new SkillLoader(
            _configuration,
            _toolRegistry,
            _messageBusMock.Object,
            _loggerMock.Object
        );

        // Act
        await loader.LoadSkillsAsync(_serviceProviderMock.Object);

        // Assert
        Assert.Empty(loader.GetLoadedSkills());
    }

    [Fact]
    public async Task GetSkill_WhenSkillExists_ReturnsSkill()
    {
        // Arrange
        var loader = new SkillLoader(
            _configuration,
            _toolRegistry,
            _messageBusMock.Object,
            _loggerMock.Object
        );

        // Create a mock skill using reflection
        var mockSkill = new TestSkill("test-skill", "1.0.0");
        var loadedSkillsField = loader.GetType()
            .GetField("_loadedSkills", BindingFlags.NonPublic | BindingFlags.Instance);

        if (loadedSkillsField != null)
        {
            var skillType = typeof(SkillLoader).GetNestedType("LoadedSkill", BindingFlags.NonPublic);
            if (skillType != null)
            {
                var loadedSkill = Activator.CreateInstance(skillType);
                if (loadedSkill != null)
                {
                    skillType.GetProperty("Skill")?.SetValue(loadedSkill, mockSkill);
                    skillType.GetProperty("Tools")?.SetValue(loadedSkill, new List<IToolDefinition>());
                    skillType.GetProperty("Handlers")?.SetValue(loadedSkill, new List<IMessageHandler>());

                    var loadedSkillsList = (System.Collections.IList?)loadedSkillsField.GetValue(loader);
                    loadedSkillsList?.Add(loadedSkill);
                }
            }
        }

        // Act
        var skill = loader.GetSkill("test-skill");

        // Assert
        Assert.NotNull(skill);
        Assert.Equal("test-skill", skill.Name);
    }

    [Fact]
    public async Task GetSkill_WhenSkillNotExists_ReturnsNull()
    {
        // Arrange
        var loader = new SkillLoader(
            _configuration,
            _toolRegistry,
            _messageBusMock.Object,
            _loggerMock.Object
        );

        // Act
        var skill = loader.GetSkill("nonexistent");

        // Assert
        Assert.Null(skill);
    }

    [Fact]
    public async Task UnloadSkillsAsync_ClearsLoadedSkills()
    {
        // Arrange
        var loader = new SkillLoader(
            _configuration,
            _toolRegistry,
            _messageBusMock.Object,
            _loggerMock.Object
        );

        // Create a mock skill using reflection
        var mockSkill = new Mock<ISkill>();
        mockSkill.Setup(s => s.Name).Returns("test");
        mockSkill.Setup(s => s.ShutdownAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var loadedSkillsField = loader.GetType()
            .GetField("_loadedSkills", BindingFlags.NonPublic | BindingFlags.Instance);

        if (loadedSkillsField != null)
        {
            var skillType = typeof(SkillLoader).GetNestedType("LoadedSkill", BindingFlags.NonPublic);
            if (skillType != null)
            {
                var loadedSkill = Activator.CreateInstance(skillType);
                if (loadedSkill != null)
                {
                    skillType.GetProperty("Skill")?.SetValue(loadedSkill, mockSkill.Object);
                    skillType.GetProperty("Tools")?.SetValue(loadedSkill, new List<IToolDefinition>());
                    skillType.GetProperty("Handlers")?.SetValue(loadedSkill, new List<IMessageHandler>());

                    var loadedSkillsList = (System.Collections.IList?)loadedSkillsField.GetValue(loader);
                    loadedSkillsList?.Add(loadedSkill);
                }
            }
        }

        // Act
        await loader.UnloadSkillsAsync();

        // Assert
        Assert.Empty(loader.GetLoadedSkills());
        mockSkill.Verify(s => s.ShutdownAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Test skill implementation for unit tests
    /// </summary>
    private class TestSkill : ISkill
    {
        public string Name { get; }
        public string Description { get; }
        public string Version { get; }

        public TestSkill(string name, string version)
        {
            Name = name;
            Description = $"Test skill {name}";
            Version = version;
        }

        public IEnumerable<IToolDefinition> GetTools()
        {
            return Enumerable.Empty<IToolDefinition>();
        }

        public IEnumerable<IMessageHandler> GetMessageHandlers()
        {
            return Enumerable.Empty<IMessageHandler>();
        }

        public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
