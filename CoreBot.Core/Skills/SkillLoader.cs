using System.Reflection;
using CoreBot.Core.Configuration;
using CoreBot.Core.Messaging;
using CoreBot.Core.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreBot.Core.Skills;

/// <summary>
/// Loads and manages plugin skills from .NET assemblies
/// </summary>
public class SkillLoader : IHostedService
{
    private readonly SkillsConfiguration _configuration;
    private readonly ToolRegistry _toolRegistry;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<SkillLoader> _logger;
    private readonly List<LoadedSkill> _loadedSkills;
    private readonly SemaphoreSlim _loadLock;
    private readonly List<Task> _handlerTasks;
    private readonly CancellationTokenSource _shutdownCts;
    private Task? _processingTask;

    public SkillLoader(
        SkillsConfiguration configuration,
        ToolRegistry toolRegistry,
        IMessageBus messageBus,
        ILogger<SkillLoader> logger)
    {
        _configuration = configuration;
        _toolRegistry = toolRegistry;
        _messageBus = messageBus;
        _logger = logger;
        _loadedSkills = new List<LoadedSkill>();
        _loadLock = new SemaphoreSlim(1, 1);
        _handlerTasks = new List<Task>();
        _shutdownCts = new CancellationTokenSource();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SkillLoader starting");
        _processingTask = RunAsync(_shutdownCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SkillLoader stopping");
        _shutdownCts.Cancel();

        await UnloadSkillsAsync(cancellationToken);

        if (_processingTask != null)
        {
            await _processingTask;
        }

        _shutdownCts.Dispose();
    }

    /// <summary>
    /// Run background message processing for skill handlers
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    /// <summary>
    /// Load all skills from the skills directory
    /// </summary>
    public async Task LoadSkillsAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        if (!_configuration.EnableSkills)
        {
            _logger.LogInformation("Skills are disabled in configuration");
            return;
        }

        await _loadLock.WaitAsync(ct);
        try
        {
            var skillsDirectory = _configuration.SkillsDirectory.Replace("~",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            if (!Directory.Exists(skillsDirectory))
            {
                _logger.LogInformation("Skills directory does not exist: {SkillsDirectory}", skillsDirectory);
                return;
            }

            var dllFiles = Directory.GetFiles(skillsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            _logger.LogInformation("Found {Count} skill DLL files in {SkillsDirectory}",
                dllFiles.Length, skillsDirectory);

            foreach (var dllFile in dllFiles)
            {
                await LoadSkillAsync(dllFile, serviceProvider, ct);
            }

            _logger.LogInformation("Successfully loaded {LoadedCount}/{TotalCount} skills",
                _loadedSkills.Count, dllFiles.Length);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Load a single skill from a DLL file
    /// </summary>
    private async Task LoadSkillAsync(string dllPath, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var fileName = Path.GetFileNameWithoutExtension(dllPath);

        try
        {
            // Check if skill is disabled
            if (_configuration.DisabledSkills.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Skill {FileName} is disabled, skipping", fileName);
                return;
            }

            // Check if only specific skills are enabled
            if (_configuration.EnabledSkills.Length > 0 &&
                !_configuration.EnabledSkills.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Skill {FileName} is not in enabled list, skipping", fileName);
                return;
            }

            _logger.LogInformation("Loading skill from {DllPath}", dllPath);

            // Load the assembly
            var assembly = Assembly.LoadFrom(dllPath);

            // Find all ISkill implementations
            var skillTypes = assembly.GetTypes()
                .Where(t => typeof(ISkill).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            if (skillTypes.Count == 0)
            {
                _logger.LogWarning("No ISkill implementations found in {DllPath}", dllPath);
                return;
            }

            foreach (var skillType in skillTypes)
            {
                await LoadSkillInstanceAsync(skillType, serviceProvider, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load skill from {DllPath}", dllPath);
            // Continue loading other skills (failure isolation)
        }
    }

    /// <summary>
    /// Load and initialize a skill instance
    /// </summary>
    private async Task LoadSkillInstanceAsync(Type skillType, IServiceProvider serviceProvider, CancellationToken ct)
    {
        try
        {
            // Create skill instance
            var skill = (ISkill?)Activator.CreateInstance(skillType);
            if (skill == null)
            {
                _logger.LogWarning("Failed to create instance of skill {SkillName}", skillType.Name);
                return;
            }

            _logger.LogInformation("Initializing skill {SkillName} v{Version}",
                skill.Name, skill.Version);

            // Initialize the skill
            await skill.InitializeAsync(serviceProvider, ct);

            // Register tools
            var tools = skill.GetTools().ToList();
            foreach (var tool in tools)
            {
                _toolRegistry.RegisterTool(tool);
                _logger.LogDebug("Registered tool {ToolName} from skill {SkillName}",
                    tool.Name, skill.Name);
            }

            // Register message handlers (stored for later retrieval)
            var handlers = skill.GetMessageHandlers().ToList();
            foreach (var handler in handlers)
            {
                _logger.LogDebug("Registered handler for {MessageType} from skill {SkillName}",
                    handler.MessageType.Name, skill.Name);
            }

            var loadedSkill = new LoadedSkill
            {
                Skill = skill,
                Tools = tools,
                Handlers = handlers
            };

            _loadedSkills.Add(loadedSkill);

            _logger.LogInformation("Successfully loaded skill {SkillName} with {ToolCount} tools and {HandlerCount} handlers",
                skill.Name, tools.Count, handlers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load skill instance {SkillName}", skillType.Name);
            // Continue loading other skills (failure isolation)
        }
    }

    /// <summary>
    /// Unload all skills
    /// </summary>
    public async Task UnloadSkillsAsync(CancellationToken ct = default)
    {
        await _loadLock.WaitAsync(ct);
        try
        {
            _logger.LogInformation("Unloading {Count} skills", _loadedSkills.Count);

            foreach (var loadedSkill in _loadedSkills)
            {
                try
                {
                    await loadedSkill.Skill.ShutdownAsync(ct);
                    _logger.LogInformation("Unloaded skill {SkillName}", loadedSkill.Skill.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error unloading skill {SkillName}", loadedSkill.Skill.Name);
                }
            }

            _loadedSkills.Clear();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Get all loaded skills
    /// </summary>
    public IEnumerable<ISkill> GetLoadedSkills()
    {
        return _loadedSkills.Select(ls => ls.Skill);
    }

    /// <summary>
    /// Get a skill by name
    /// </summary>
    public ISkill? GetSkill(string name)
    {
        return _loadedSkills
            .Select(ls => ls.Skill)
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all message handlers for a specific message type
    /// </summary>
    public IEnumerable<IMessageHandler> GetMessageHandlers(Type messageType)
    {
        return _loadedSkills
            .SelectMany(ls => ls.Handlers)
            .Where(h => h.MessageType == messageType);
    }

    /// <summary>
    /// Information about a loaded skill
    /// </summary>
    private class LoadedSkill
    {
        public required ISkill Skill { get; init; }
        public required List<IToolDefinition> Tools { get; init; }
        public required List<IMessageHandler> Handlers { get; init; }
    }
}
