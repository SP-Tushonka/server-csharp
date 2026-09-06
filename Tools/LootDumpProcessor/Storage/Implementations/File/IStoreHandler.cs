namespace LootDumpProcessor.Storage.Implementations.File;

public interface IStoreHandler
{
    void Store<T>(T obj, bool failIfDuplicate = true)
        where T : IKeyable;
    T? Retrieve<T>(IKey obj)
        where T : IKeyable;
    bool Exists(IKey obj);
    List<T> RetrieveAll<T>()
        where T : IKeyable;
}
