using System.Text.Json.Serialization;
using LootDumpProcessor.Utils;

namespace LootDumpProcessor.Storage.Collections;

public class SubdivisionedKeyableDictionary<K, V> : Dictionary<K, V>, IKeyable
{
    [JsonPropertyName("__id__")]
    public string __ID { get; set; } = KeyGenerator.GetNextKey();

    [JsonPropertyName("extras")]
    public string? Extras
    {
        get
        {
            if (ExtraSubdivisions == null)
                return null;
            return string.Join(",", ExtraSubdivisions);
        }
        set { ExtraSubdivisions = value.Split(","); }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    private string[]? ExtraSubdivisions { get; set; }

    public IKey GetKey()
    {
        if (ExtraSubdivisions != null)
        {
            var subdivisions = new List<string> { "dictionaries" };
            subdivisions.AddRange(ExtraSubdivisions);
            subdivisions.Add(__ID);
            return new SubdivisionedUniqueKey(subdivisions.ToArray());
        }

        return new SubdivisionedUniqueKey(["dictionaries", __ID]);
    }

    public void AddExtraSubdivisions(string[] extras)
    {
        ExtraSubdivisions = extras;
    }
}
