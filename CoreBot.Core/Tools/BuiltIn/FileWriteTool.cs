using System.Text.Json;

namespace CoreBot.Core.Tools.BuiltIn;

/// <summary>
/// Tool for writing files within the workspace
/// </summary>
public class FileWriteTool : IToolDefinition
{
    private readonly ToolRegistry _registry;

    public FileWriteTool(ToolRegistry registry)
    {
        _registry = registry;
    }

    public string Name => "file_write";

    public string Description => "Write content to a file within the workspace";

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
                    description = "Path to the file to write (relative to workspace)"
                },
                content = new
                {
                    type = "string",
                    description = "Content to write to the file"
                }
            },
            required = new[] { "path", "content" }
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

            if (!parameters.TryGetProperty("content", out var contentElement))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = "Missing required parameter: content"
                };
            }

            var path = pathElement.GetString();
            var content = contentElement.GetString();

            if (string.IsNullOrWhiteSpace(path))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = "Path parameter cannot be empty"
                };
            }

            if (content == null)
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = "Content parameter cannot be null"
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

            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write file
            await File.WriteAllTextAsync(fullPath, content, ct);

            return new ToolResult
            {
                Success = true,
                Result = $"Successfully wrote {content.Length} characters to {path}"
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Result = string.Empty,
                Error = $"Failed to write file: {ex.Message}"
            };
        }
    }
}
