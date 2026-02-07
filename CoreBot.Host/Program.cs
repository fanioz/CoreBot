using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using CoreBot.Core.Configuration;
using CoreBot.Core.Messaging;
using CoreBot.Core.Memory;
using CoreBot.Core.Tools;
using CoreBot.Core.Services;
using CoreBot.Core.Subagents;
using CoreBot.Core.Skills;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        // Load appsettings.json first (application defaults)
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        // Load user configuration from ~/.nanobot/config.json (highest priority file-based)
        var userConfigPath = ConfigurationHelper.GetNanobotConfigPath();
        if (File.Exists(userConfigPath))
        {
            config.AddJsonFile(userConfigPath, optional: false, reloadOnChange: true);
        }
        else
        {
            // Create default configuration if it doesn't exist
            Console.WriteLine($"Configuration file not found at {userConfigPath}");
            Console.WriteLine("Creating default configuration file...");
            ConfigurationHelper.CreateDefaultConfigurationAsync(userConfigPath)
                .GetAwaiter()
                .GetResult();
            Console.WriteLine($"Default configuration created at {userConfigPath}");
            Console.WriteLine("Please edit the configuration file and restart Nanobot.");
            Environment.Exit(0);
        }

        // Add environment variables (highest priority)
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // Configure CoreBotConfiguration
        services.AddOptions<CoreBotConfiguration>()
            .Bind(context.Configuration.GetSection("CoreBot"))
            .Validate(config =>
            {
                var validator = new CoreBotConfigurationValidator();
                var result = validator.Validate(nameof(CoreBotConfiguration), config);
                return result.Succeeded;
            })
            .ValidateOnStart();

        // Register core infrastructure services
        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<IMemoryStore, FileMemoryStore>();
        services.AddSingleton<ToolRegistry>();

        // Register hosted services (background services)
        services.AddHostedService<AgentService>();
        services.AddHostedService<SchedulerService>();
        services.AddHostedService<SubagentManager>();
        services.AddHostedService<SkillLoader>();
    })
    .ConfigureLogging((context, logging) =>
    {
        // Get log level from configuration
        var logLevel = context.Configuration["Nanobot:Logging:Level"] ?? "Information";

        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    // Enable BOTH Windows Service and systemd support
    // These will auto-detect the runtime environment
    .UseWindowsService()
    .UseSystemd()
    .Build();

await host.RunAsync();
