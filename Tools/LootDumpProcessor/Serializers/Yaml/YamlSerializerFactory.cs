namespace LootDumpProcessor.Serializers.Yaml;

public static class YamlSerializerFactory
{
    private static IYamlSerializer? _instance;

    public static IYamlSerializer GetInstance()
    {
        if (_instance == null)
        {
            _instance = new YamlDotNetYamlSerializer();
        }

        return _instance;
    }
}
