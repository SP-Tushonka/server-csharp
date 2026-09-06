using System.Text.Json.Serialization;
using LootDumpProcessor.Process.Reader;
using LootDumpProcessor.Process.Reader.Intake;

namespace LootDumpProcessor.Model.Config;

public class IntakeReaderConfig
{
    [JsonPropertyName("readerType")]
    public IntakeReaderTypes IntakeReaderType { get; set; } = IntakeReaderTypes.Json;

    [JsonPropertyName("maxDumpsPerMap")]
    public int MaxDumpsPerMap { get; set; } = 1500;

    [JsonPropertyName("ignoredDumpLocations")]
    public List<string> IgnoredDumpLocations { get; set; } = new();
}
