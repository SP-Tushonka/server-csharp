namespace LootDumpProcessor.Process.Reader.Filters;

public interface IFileFilter
{
    string GetExtension();
    bool Accept(string filename);
}
