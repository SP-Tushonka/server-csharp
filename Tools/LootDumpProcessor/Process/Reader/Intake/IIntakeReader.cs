using LootDumpProcessor.Model.Processing;

namespace LootDumpProcessor.Process.Reader.Intake;

public interface IIntakeReader
{
    bool Read(string file, out BasicInfo basicInfo);
}
