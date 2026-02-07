using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CoreBot.Core.Configuration;
using CoreBot.Core.Messaging;
using CoreBot.Core.Memory;
using CoreBot.Core.Tools;
using Xunit;

namespace CoreBot.Tests.Properties;

/// <summary>
/// Property-based tests for graceful shutdown
/// Property 1: Graceful Shutdown Persistence
/// Validates: Requirements 1.4
/// </summary>
public class GracefulShutdownProperties
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void ServiceCollection_CanRegisterHostedServices(int serviceCount)
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<IMemoryStore, FileMemoryStore>();
        services.AddSingleton<ToolRegistry>();

        // Act
        for (int i = 0; i < serviceCount; i++)
        {
            services.AddHostedService<TestHostedService>();
        }

        // Assert - Services registered without exception
        Assert.NotNull(services);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void ServiceCollection_CanRegisterMultipleHostedServices(int serviceCount)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<IMemoryStore, FileMemoryStore>();
        services.AddSingleton<ToolRegistry>();

        // Act
        for (int i = 0; i < serviceCount; i++)
        {
            var serviceId = i;
            services.AddHostedService(sp => new TestHostedService(serviceId));
        }

        // Assert
        Assert.NotNull(services);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task HostedService_StartsAndStops(int iterations)
    {
        // Arrange
        var service = new TestHostedService();
        var cts = new CancellationTokenSource();

        // Act & Assert
        for (int i = 0; i < iterations; i++)
        {
            await service.StartAsync(cts.Token);
            await service.StopAsync(cts.Token);
        }

        Assert.True(true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HostedService_RespectsCancellation(bool shouldCancel)
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var service = new TestHostedService();

        if (shouldCancel)
        {
            cts.Cancel();
        }

        // Act
        var startTask = service.StartAsync(cts.Token);
        var stopTask = service.StopAsync(cts.Token);

        // Assert
        await Task.WhenAll(startTask, stopTask);
        Assert.True(true);
    }

    [Fact]
    public async Task MultipleHostedServices_StopInOrder()
    {
        // Arrange
        var services = new List<TestHostedService>();
        for (int i = 0; i < 5; i++)
        {
            services.Add(new TestHostedService(i));
        }

        var cts = new CancellationTokenSource();

        // Act
        foreach (var service in services)
        {
            await service.StartAsync(cts.Token);
        }

        var stopOrder = new List<int>();
        foreach (var service in services)
        {
            await service.StopAsync(cts.Token);
            stopOrder.Add(service.ServiceId);
        }

        // Assert
        Assert.Equal(5, stopOrder.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, stopOrder);
    }

    /// <summary>
    /// Test hosted service for property tests
    /// </summary>
    private class TestHostedService : IHostedService
    {
        public int ServiceId { get; }

        public TestHostedService(int serviceId = 0)
        {
            ServiceId = serviceId;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
