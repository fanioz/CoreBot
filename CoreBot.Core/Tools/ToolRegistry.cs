using System.Text.Json;
using CoreBot.Core.Configuration;

namespace CoreBot.Core.Tools;

/// <summary>
/// Registry for managing and executing tools
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, IToolDefinition> _tools;
    private readonly string _workspacePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ToolRegistry(ToolConfiguration configuration)
    {
        _tools = new Dictionary<string, IToolDefinition>(StringComparer.OrdinalIgnoreCase);
        _workspacePath = configuration.WorkspacePath ?? "~/.corebot/workspace";
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Register a tool in the registry
    /// </summary>
    public void RegisterTool(IToolDefinition tool)
    {
        if (_tools.ContainsKey(tool.Name))
        {
            throw new InvalidOperationException($"Tool '{tool.Name}' is already registered.");
        }

        _tools[tool.Name] = tool;
    }

    /// <summary>
    /// Get a tool by name
    /// </summary>
    public IToolDefinition? GetTool(string name)
    {
        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    /// <summary>
    /// Get all registered tools
    /// </summary>
    public IEnumerable<IToolDefinition> GetAllTools()
    {
        return _tools.Values;
    }

    /// <summary>
    /// Execute a tool with parameters
    /// </summary>
    public virtual async Task<ToolResult> ExecuteAsync(string toolName, JsonElement parameters, CancellationToken ct = default)
    {
        // Validate tool exists
        var tool = GetTool(toolName);
        if (tool == null)
        {
            return new ToolResult
            {
                Success = false,
                Result = string.Empty,
                Error = $"Tool '{toolName}' not found."
            };
        }

        // Validate parameters against schema
        var validationResult = ValidateParameters(tool.GetSchema(), parameters);
        if (!validationResult.Success)
        {
            return new ToolResult
            {
                Success = false,
                Result = string.Empty,
                Error = validationResult.Error
            };
        }

        // Execute tool
        try
        {
            return await tool.ExecuteAsync(parameters, ct);
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Result = string.Empty,
                Error = $"Tool execution failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validate parameters against JSON schema
    /// </summary>
    private (bool Success, string? Error) ValidateParameters(JsonDocument schema, JsonElement parameters)
    {
        // Basic validation - check if parameters is an object
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            return (false, "Parameters must be a JSON object");
        }

        // Get properties schema
        if (!schema.RootElement.TryGetProperty("properties", out var propertiesSchema))
        {
            // No properties defined, parameters are optional
            return (true, null);
        }

        // Validate required properties
        if (schema.RootElement.TryGetProperty("required", out var requiredProps))
        {
            foreach (var requiredProp in requiredProps.EnumerateArray())
            {
                var propName = requiredProp.GetString();
                if (string.IsNullOrEmpty(propName))
                {
                    continue;
                }

                if (!parameters.TryGetProperty(propName, out _))
                {
                    return (false, $"Required parameter '{propName}' is missing");
                }
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Validate that a path is within the workspace
    /// </summary>
    public bool IsPathInWorkspace(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Expand workspace path (handle ~)
        var expandedWorkspace = _workspacePath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var fullPath = Path.GetFullPath(path);

        // For relative paths, resolve against current directory
        if (!Path.IsPathRooted(path))
        {
            fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        var workspaceFullPath = Path.GetFullPath(expandedWorkspace);

        // Check if the path is within the workspace
        return fullPath.StartsWith(workspaceFullPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get the full workspace path
    /// </summary>
    public string GetWorkspacePath()
    {
        return _workspacePath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }
}
