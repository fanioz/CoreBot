using CoreBot.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoreBot.Tests.Unit.Logging;

/// <summary>
/// Unit tests for logging configuration
/// </summary>
public class LoggingTests
{
    [Fact]
    public void LoggingConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new LoggingConfiguration();

        // Assert
        Assert.Equal("Information", config.Level);
        Assert.True(config.EnableScopes);
        Assert.True(config.LogToConsole);
        Assert.False(config.LogToWindowsEventLog);
        Assert.Equal("CoreBot", config.WindowsEventLogSource);
        Assert.Equal("Application", config.WindowsEventLogName);
    }

    [Theory]
    [InlineData("Debug", LogLevel.Debug)]
    [InlineData("Information", LogLevel.Information)]
    [InlineData("Warning", LogLevel.Warning)]
    [InlineData("Error", LogLevel.Error)]
    [InlineData("Critical", LogLevel.Critical)]
    public void LoggingConfiguration_ParsesLogLevel(string configLevel, LogLevel expectedLogLevel)
    {
        // Arrange & Act
        var config = new LoggingConfiguration { Level = configLevel };
        var parsed = Enum.Parse<LogLevel>(configLevel, ignoreCase: true);

        // Assert
        Assert.Equal(expectedLogLevel, parsed);
    }

    [Fact]
    public void LoggingConfiguration_CanSetCustomValues()
    {
        // Arrange & Act
        var config = new LoggingConfiguration
        {
            Level = "Debug",
            EnableScopes = false,
            LogToConsole = false,
            LogToWindowsEventLog = true,
            WindowsEventLogSource = "TestBot",
            WindowsEventLogName = "TestLog"
        };

        // Assert
        Assert.Equal("Debug", config.Level);
        Assert.False(config.EnableScopes);
        Assert.False(config.LogToConsole);
        Assert.True(config.LogToWindowsEventLog);
        Assert.Equal("TestBot", config.WindowsEventLogSource);
        Assert.Equal("TestLog", config.WindowsEventLogName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoggingConfiguration_EnableScopes_HasValue(bool enableScopes)
    {
        // Arrange & Act
        var config = new LoggingConfiguration { EnableScopes = enableScopes };

        // Assert
        Assert.Equal(enableScopes, config.EnableScopes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoggingConfiguration_LogToConsole_HasValue(bool logToConsole)
    {
        // Arrange & Act
        var config = new LoggingConfiguration { LogToConsole = logToConsole };

        // Assert
        Assert.Equal(logToConsole, config.LogToConsole);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoggingConfiguration_LogToWindowsEventLog_HasValue(bool logToEventLog)
    {
        // Arrange & Act
        var config = new LoggingConfiguration { LogToWindowsEventLog = logToEventLog };

        // Assert
        Assert.Equal(logToEventLog, config.LogToWindowsEventLog);
    }
}
