namespace DeviceDesk.Modules.Phase1.Services.DocumentIngest;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>API key for https://api.anthropic.com — leave empty to use heuristic classification only.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-3-5-haiku-20241022";

    public int MaxTokens { get; set; } = 4096;
}
