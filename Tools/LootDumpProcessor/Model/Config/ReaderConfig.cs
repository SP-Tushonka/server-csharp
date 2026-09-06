using System.Text.Json.Serialization;
using LootDumpProcessor.Process.Reader.Filters;

namespace LootDumpProcessor.Model.Config;

public class ReaderConfig
{
    [JsonPropertyName("intakeReaderConfig")]
    public IntakeReaderConfig? IntakeReaderConfig { get; set; }

    [JsonPropertyName("thresholdDate")]
    public string? ThresholdDate { get; set; }

    [JsonPropertyName("acceptedFileExtensions")]
    public List<string> AcceptedFileExtensions { get; set; } = new();

    [JsonPropertyName("processSubFolders")]
    public bool ProcessSubFolders { get; set; }

    [JsonPropertyName("fileFilters")]
    public List<FileFilterTypes>? FileFilters { get; set; }
}
