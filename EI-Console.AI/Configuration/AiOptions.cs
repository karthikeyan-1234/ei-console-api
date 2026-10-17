namespace EI_Console.AI.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "AI";
    public string Endpoint { get; set; } = "http://localhost:11434/v1";
    public string Model { get; set; } = "qwen3.5:4b";
    public string ApiKey { get; set; } = "ollama";
    public int MaxOutputTokens { get; set; } = 2048;
}