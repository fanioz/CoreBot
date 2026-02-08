namespace CoreBot.Core.Configuration;

/// <summary>
/// Configuration for the skills plugin system
/// </summary>
public class SkillsConfiguration
{
    /// <summary>
    /// Directory containing skill DLLs
    /// </summary>
    public string SkillsDirectory { get; set; } = "~/.corebot/skills";

    /// <summary>
    /// Whether to load skills on startup
    /// </summary>
    public bool EnableSkills { get; set; } = true;

    /// <summary>
    /// Specific skill names to load (empty = load all)
    /// </summary>
    public string[] EnabledSkills { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Skill names to exclude from loading
    /// </summary>
    public string[] DisabledSkills { get; set; } = Array.Empty<string>();
}
