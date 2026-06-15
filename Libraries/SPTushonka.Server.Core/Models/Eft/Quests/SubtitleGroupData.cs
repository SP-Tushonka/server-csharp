using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public sealed class SubtitleGroupData
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("subtitles")]
    public required List<SubtitleTiming> Subtitles { get; set; }
}

public sealed class SubtitleTiming
{
    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("start")]
    public float Start { get; set; }

    [JsonPropertyName("end")]
    public float End { get; set; }
}
