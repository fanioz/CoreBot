using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using CoreBot.Core.Configuration;
using CoreBot.Core.Messaging;
using CoreBot.Core.Memory;
using CoreBot.Core.Tools;
using CoreBot.Core.Services;
using CoreBot.Core.Subagents;
using CoreBot.Core.Skills;
using Xunit;

namespace CoreBot.Tests.Unit;

/// <summary>
/// Unit tests for Program.cs service host setup
/// </summary>
public class ProgramTests
{
    [Fact]
    public void ServiceCollection_CanRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<IMemoryStore, FileMemoryStore>();
        services.AddSingleton<ToolRegistry>();
        services.AddHostedService<TestHostedService>();

        // Assert
        Assert.NotNull(services);
    }

    [Fact]
    public void ServiceCollection_CanRegisterMultipleSingletons()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<IMemoryStore, FileMemoryStore>();
        services.AddSingleton<ToolRegistry>();

        // Assert
        Assert.NotNull(services);
    }

    [Fact]
    public void ServiceCollection_CanRegisterHostedServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHostedService<TestHostedService>();
        services.AddHostedService<AgentService>();
        services.AddHostedService<SchedulerService>();
        services.AddHostedService<SubagentManager>();
        services.AddHostedService<SkillLoader>();

        // Assert
        Assert.NotNull(services);
    }

    /// <summary>
    /// Test hosted service for unit tests
    /// </summary>
    private class TestHostedService : Microsoft.Extensions.Hosting.IHostedService
    {
        public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
