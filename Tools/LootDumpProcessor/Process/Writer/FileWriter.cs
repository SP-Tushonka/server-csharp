using LootDumpProcessor.Serializers.Json;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LootDumpProcessor.Process.Writer;

public class FileWriter : IWriter
{
    private static readonly IJsonSerializer _jsonSerializer = new NetJsonSerializer();
    private static readonly string _outputPath;

    static FileWriter()
    {
        var path = LootDumpProcessorContext.OutputDirectory;
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        _outputPath = path;
    }

    public void WriteAll(Dictionary<OutputFileType, object> dumpData)
    {
        foreach (var (key, value) in dumpData)
        {
            Write(key, value);
        }
    }

    public void Write(OutputFileType type, object data)
    {
        if (!Directory.Exists($"{_outputPath}\\loot"))
            Directory.CreateDirectory($"{_outputPath}\\loot");
        switch (type)
        {
            case OutputFileType.LooseLoot:
                var looseLootData = (Dictionary<string, LooseLoot>)data;
                foreach (var (key, value) in looseLootData)
                {
                    if (!Directory.Exists($@"{_outputPath}\locations\{key}"))
                        Directory.CreateDirectory($@"{_outputPath}\locations\{key}");
                    File.WriteAllText($@"{_outputPath}\locations\{key}\looseLoot.json", _jsonSerializer.Serialize(value));
                }

                break;
            case OutputFileType.StaticContainer:
                var staticContainer = (Dictionary<string, StaticContainerDetails>)data;
                foreach (var (key, value) in staticContainer)
                {
                    if (!Directory.Exists($@"{_outputPath}\locations\{key}"))
                        Directory.CreateDirectory($@"{_outputPath}\locations\{key}");
                    File.WriteAllText($@"{_outputPath}\locations\{key}\staticContainers.json", _jsonSerializer.Serialize(value));
                }

                break;
            case OutputFileType.StaticLoot:
                var staticLootData = (Dictionary<string, Dictionary<string, StaticLootDetails>>)data;
                foreach (var (key, value) in staticLootData)
                {
                    if (!Directory.Exists($@"{_outputPath}\locations\{key}"))
                        Directory.CreateDirectory($@"{_outputPath}\locations\{key}");
                    File.WriteAllText($@"{_outputPath}\locations\{key}\staticLoot.json", _jsonSerializer.Serialize(value));
                }

                break;
            case OutputFileType.StaticAmmo:
                var staticAmmo = (Dictionary<string, Dictionary<string, List<StaticAmmoDetails>>>)data;
                foreach (var (key, value) in staticAmmo)
                {
                    if (!Directory.Exists($@"{_outputPath}\locations\{key}"))
                        Directory.CreateDirectory($@"{_outputPath}\locations\{key}");
                    File.WriteAllText($@"{_outputPath}\locations\{key}\staticAmmo.json", _jsonSerializer.Serialize(value));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}
