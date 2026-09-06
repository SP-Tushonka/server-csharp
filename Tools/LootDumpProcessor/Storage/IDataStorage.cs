namespace LootDumpProcessor.Storage;

public interface IDataStorage
{
    void Setup();
    void Store<T>(T t)
        where T : IKeyable;
    bool Exists(IKey t);
    T GetItem<T>(IKey key)
        where T : IKeyable;
    List<T> GetAll<T>();
    void Clear();
}
