namespace LootDumpProcessor.Process.Reader.Intake;

public static class IntakeReaderFactory
{
    private static readonly Dictionary<IntakeReaderTypes, IIntakeReader> Instances = new();
    private static readonly object DictionaryLock = new();

    public static IIntakeReader GetInstance()
    {
        var type = LootDumpProcessorContext.GetConfig().ReaderConfig.IntakeReaderConfig?.IntakeReaderType ?? IntakeReaderTypes.Json;
        lock (DictionaryLock)
        {
            if (!Instances.TryGetValue(type, out var intakeReader))
            {
                intakeReader = type switch
                {
                    IntakeReaderTypes.Json => new JsonFileIntakeReader(),
                    _ => throw new ArgumentOutOfRangeException("IntakeReaderType", "Value was not defined on IntakeReaderConfig"),
                };
                Instances.Add(type, intakeReader);
            }
            return intakeReader;
        }
    }
}
