using System.Text.Json;

namespace CoreBot.Core.Tools.BuiltIn;

/// <summary>
/// Tool for reading files within the workspace
/// </summary>
public class FileReadTool : IToolDefinition
{
    private readonly ToolRegistry _registry;

    public FileReadTool(ToolRegistry registry)
    {
        _registry = registry;
    }

    public string Name => "file_read";

    public string Description => "Read the contents of a file within the workspace";

    public JsonDocument GetSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Path to the file to read (relative to workspace)"
                }
            },
            required = new[] { "path" }
        };

        return JsonDocument.Parse(JsonSerializer.Serialize(schema));
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        try
        {
            // Extract parameters
            if (!parameters.TryGetProperty("path", out var pathElement))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = "Missing required parameter: path"
                };
            }

            var path = pathElement.GetString();
            if (string.IsNullOrWhiteSpace(path))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = "Path parameter cannot be empty"
                };
            }

            // Validate path is within workspace
            if (!_registry.IsPathInWorkspace(path))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = $"Path '{path}' is outside the workspace directory"
                };
            }

            // Expand workspace path and resolve full path
            var workspacePath = _registry.GetWorkspacePath();
            var fullPath = Path.Combine(workspacePath, path);

            // Check if file exists
            if (!File.Exists(fullPath))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = $"File not found: {path}"
                };
            }

            // Read file contents
            var content = await File.ReadAllTextAsync(fullPath, ct);

            return new ToolResult
            {
                Success = true,
                Result = content
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Result = string.Empty,
                Error = $"Failed to read file: {ex.Message}"
            };
        }
    }
}
