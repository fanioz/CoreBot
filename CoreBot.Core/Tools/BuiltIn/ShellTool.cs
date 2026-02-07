using System.Diagnostics;
using System.Text.Json;

namespace CoreBot.Core.Tools.BuiltIn;

/// <summary>
/// Tool for executing shell commands with timeout enforcement
/// </summary>
public class ShellTool : IToolDefinition
{
    private readonly ToolRegistry _registry;
    private readonly int _timeoutSeconds;

    public ShellTool(ToolRegistry registry, int timeoutSeconds = 30)
    {
        _registry = registry;
        _timeoutSeconds = timeoutSeconds;
    }

    public string Name => "shell";

    public string Description => $"Execute a shell command (max {_timeoutSeconds} seconds)";

    public JsonDocument GetSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                command = new
                {
                    type = "string",
                    description = "Shell command to execute"
                }
            },
            required = new[] { "command" }
        };

        return JsonDocument.Parse(JsonSerializer.Serialize(schema));
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default)
    {
        try
        {
            // Extract parameters
            if (!parameters.TryGetProperty("command", out var commandElement))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = "Missing required parameter: command"
                };
            }

            var command = commandElement.GetString();
            if (string.IsNullOrWhiteSpace(command))
            {
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = "Command parameter cannot be empty"
                };
            }

            // Create process
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            // Start process
            process.Start();

            // Wait for completion with timeout
            var completed = process.WaitForExit(_timeoutSeconds * 1000);

            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                return new ToolResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = $"Command timed out after {_timeoutSeconds} seconds"
                };
            }

            // Read output
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            var output = stdout;
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                output += $"\nStderr: {stderr}";
            }

            return new ToolResult
            {
                Success = process.ExitCode == 0,
                Result = output.Trim(),
                Error = process.ExitCode != 0 ? $"Command failed with exit code {process.ExitCode}" : null
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Result = string.Empty,
                Error = $"Failed to execute command: {ex.Message}"
            };
        }
    }
}
