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
using CoreBot.Core.LLM;
using CoreBot.Core.LLM.Clients;
using CoreBot.Core.ChatPlatforms;

// Check for CLI mode
var isCliMode = args.Contains("--cli") || args.Contains("--message");
var messageToSend = args.SkipWhile(a => a != "--message").Skip(1).FirstOrDefault();

if (isCliMode)
{
    // CLI mode - run single message or interactive
    await RunCliModeAsync(args);
}
else
{
    // Daemon mode - run as service
    await RunDaemonModeAsync(args);
}

return;

// CLI Mode Handler
async Task RunCliModeAsync(string[] args)
{
    Console.WriteLine("CoreBot CLI Mode");
    Console.WriteLine();

    // Build minimal host for CLI
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var userConfigPath = ConfigurationHelper.GetCorebotConfigPath();
            if (File.Exists(userConfigPath))
            {
                config.AddJsonFile(userConfigPath, optional: true, reloadOnChange: true);
            }
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            // Configure options
            services.AddOptions<CoreBotConfiguration>()
                .Bind(context.Configuration.GetSection("CoreBot"));

            services.AddOptions<LlmConfiguration>()
                .Bind(context.Configuration.GetSection("CoreBot:Llm"));

            services.AddOptions<ToolConfiguration>()
                .Bind(context.Configuration.GetSection("CoreBot:Tools"));

            // Register core services
            services.AddSingleton<IMessageBus, MessageBus>();
            services.AddSingleton<IMemoryStore, FileMemoryStore>();
            services.AddSingleton<ToolRegistry>();
            services.AddHttpClient();

            // Register tool configuration
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<ToolConfiguration>>();
                return options.Value;
            });

            // Register LLM provider
            var llmConfig = context.Configuration.GetSection("CoreBot:Llm").Get<LlmConfiguration>();
            if (llmConfig != null)
            {
                var provider = llmConfig.Provider?.ToLowerInvariant() ?? "openrouter";
                var apiKey = llmConfig.ApiKey ?? string.Empty;
                var model = llmConfig.Model ?? string.Empty;
                var baseUrl = llmConfig.BaseUrl;

                ILlmProvider llmProvider = provider switch
                {
                    "openrouter" => new OpenRouterClient(new HttpClient(), apiKey, model),
                    "anthropic" => new AnthropicClient(new HttpClient(), apiKey, model, baseUrl),
                    "zai" => new AnthropicClient(new HttpClient(), apiKey, model, baseUrl),
                    "openai" => new OpenAIClient(new HttpClient(), apiKey, model),
                    "deepseek" => new DeepSeekClient(new HttpClient(), apiKey, model),
                    "groq" => new GroqClient(new HttpClient(), apiKey, model),
                    "gemini" => new GeminiClient(new HttpClient(), apiKey, model),
                    _ => new OpenRouterClient(new HttpClient(), apiKey, model)
                };
                services.AddSingleton<ILlmProvider>(llmProvider);
            }

            // Register CLI configuration
            services.AddOptions<CliConfiguration>()
                .Bind(context.Configuration.GetSection("CoreBot:ChatPlatforms:cli"));

            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<CliConfiguration>>();
                return options.Value;
            });

            // Register CLI adapter
            services.AddSingleton<CliAdapter>();

            // Register agent service for CLI mode
            services.AddHostedService<AgentService>();
        })
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        })
        .Build();

    // Start the host to run the AgentService
    await host.StartAsync();

    // Give the agent service a moment to start
    await Task.Delay(1000);

    // Get CLI adapter
    var cliAdapter = host.Services.GetRequiredService<CliAdapter>();

    if (!string.IsNullOrEmpty(messageToSend))
    {
        // Single message mode
        Console.WriteLine($"Sending: {messageToSend}");
        Console.WriteLine();
        var response = await cliAdapter.SendMessageAsync(messageToSend);
        Console.WriteLine(response);
    }
    else
    {
        // Interactive mode
        await cliAdapter.StartInteractiveAsync();
    }

    await host.StopAsync();
}

// Daemon Mode Handler
async Task RunDaemonModeAsync(string[] args)
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            // Load appsettings.json first (application defaults)
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            // Load user configuration from ~/.corebot/config.json (highest priority file-based)
            var userConfigPath = ConfigurationHelper.GetCorebotConfigPath();
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
                Console.WriteLine("Please edit the configuration file and restart CoreBot.");
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

            // Configure and register chat platform configurations
            services.Configure<TelegramConfiguration>(context.Configuration.GetSection("CoreBot:ChatPlatforms:telegram"));
            services.Configure<WhatsAppConfiguration>(context.Configuration.GetSection("CoreBot:ChatPlatforms:whatsapp"));
            services.Configure<FeishuConfiguration>(context.Configuration.GetSection("CoreBot:ChatPlatforms:feishu"));

            // Register configuration objects directly for adapters
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<TelegramConfiguration>>();
                return options.Value;
            });
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<WhatsAppConfiguration>>();
                return options.Value;
            });
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<FeishuConfiguration>>();
                return options.Value;
            });

            // Configure and register tool configuration
            services.Configure<ToolConfiguration>(context.Configuration.GetSection("CoreBot:Tools"));
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<ToolConfiguration>>();
                return options.Value;
            });

            // Configure and register skills configuration
            services.Configure<SkillsConfiguration>(context.Configuration.GetSection("CoreBot:Skills"));
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<SkillsConfiguration>>();
                return options.Value;
            });

            // Register core infrastructure services
            services.AddSingleton<IMessageBus, MessageBus>();
            services.AddSingleton<IMemoryStore, FileMemoryStore>();
            services.AddSingleton<ToolRegistry>();
            services.AddHttpClient(); // Register HttpClient for adapters

            // Register LLM provider based on configuration
            var llmConfig = context.Configuration.GetSection("CoreBot:Llm").Get<LlmConfiguration>();
            if (llmConfig != null)
            {
                var provider = llmConfig.Provider?.ToLowerInvariant() ?? "openrouter";
                var apiKey = llmConfig.ApiKey ?? string.Empty;
                var model = llmConfig.Model ?? string.Empty;
                var baseUrl = llmConfig.BaseUrl;

                switch (provider)
                {
                    case "openrouter":
                        services.AddSingleton<ILlmProvider>(provider => new OpenRouterClient(
                            provider.GetRequiredService<HttpClient>(), apiKey, model));
                        break;
                    case "anthropic":
                    case "zai":
                        services.AddSingleton<ILlmProvider>(provider => new AnthropicClient(
                            provider.GetRequiredService<HttpClient>(), apiKey, model, baseUrl));
                        break;
                    case "openai":
                        services.AddSingleton<ILlmProvider>(provider => new OpenAIClient(
                            provider.GetRequiredService<HttpClient>(), apiKey, model));
                        break;
                    case "deepseek":
                        services.AddSingleton<ILlmProvider>(provider => new DeepSeekClient(
                            provider.GetRequiredService<HttpClient>(), apiKey, model));
                        break;
                    case "groq":
                        services.AddSingleton<ILlmProvider>(provider => new GroqClient(
                            provider.GetRequiredService<HttpClient>(), apiKey, model));
                        break;
                    case "gemini":
                        services.AddSingleton<ILlmProvider>(provider => new GeminiClient(
                            provider.GetRequiredService<HttpClient>(), apiKey, model));
                        break;
                    default:
                        services.AddSingleton<ILlmProvider>(provider => new OpenRouterClient(
                            provider.GetRequiredService<HttpClient>(), apiKey, model));
                        break;
                }
            }

            // Register chat platform adapters based on configuration
            var chatPlatformsSection = context.Configuration.GetSection("CoreBot:ChatPlatforms");
            if (chatPlatformsSection.Exists())
            {
                var telegramConfig = chatPlatformsSection.GetSection("telegram").Get<ChatPlatformConfiguration>();
                if (telegramConfig != null && telegramConfig.Enabled)
                {
                    services.AddSingleton<IChatPlatform, TelegramAdapter>();
                }

                var whatsappConfig = chatPlatformsSection.GetSection("whatsapp").Get<ChatPlatformConfiguration>();
                if (whatsappConfig != null && whatsappConfig.Enabled)
                {
                    services.AddSingleton<IChatPlatform, WhatsAppAdapter>();
                }

                var feishuConfig = chatPlatformsSection.GetSection("feishu").Get<ChatPlatformConfiguration>();
                if (feishuConfig != null && feishuConfig.Enabled)
                {
                    services.AddSingleton<IChatPlatform, FeishuAdapter>();
                }

                // Register the chat platform service wrapper
                services.AddHostedService<ChatPlatformService>();
            }

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
}
