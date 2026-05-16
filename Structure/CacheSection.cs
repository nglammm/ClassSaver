namespace ClassSaver.Structure;

/// <summary>
/// Cache section used to store all the cache metadata.
/// </summary>
public class CacheSection
{
    public int CacheMode;
    
    // this will be null if (CacheMode)CacheMode != CacheMode.Keyword.
    public Dictionary<int, string> KeywordCache_WordMap = new();
}