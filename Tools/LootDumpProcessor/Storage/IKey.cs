namespace LootDumpProcessor.Storage;

public interface IKey
{
    KeyType GetKeyType();
    string[] GetLookupIndex();
}

public enum KeyType
{
    /**
     * Meant to be used as "single table" storage style
     */
    Unique,

    /**
     * Meant to be used as "relational table" storage style
     */
    Subdivisioned,
}
