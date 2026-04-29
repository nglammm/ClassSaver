namespace ClassSaver;

/// <summary>
/// Parses the class with given stream.
/// Only works if serialized with class 'ClassSerializer'.
/// </summary>
public class ClassParser
{
    private HeaderSection _headerSection;
    private CacheSection _cacheSection;
    
    
    public T Parse<T>(Stream stream)
    {
        using var reader = new BinaryReader(stream);
        
        // Reads the first section
        _headerSection = ReadSection<HeaderSection>(ClassSection.Header, reader);
        _cacheSection = ReadSection<CacheSection>(ClassSection.Cache, reader);
        
        return ReadSection<T>(ClassSection.Data, reader);
    }
    
    /// <summary>
    /// Gets the header section from the latest class to parse.
    /// </summary>
    /// <returns>The header section</returns>
    public HeaderSection GetHeaderSection() => _headerSection;
    
    /// <summary>
    /// Gets the cache section from the latest class to parse.
    /// </summary>
    /// <returns>The cache section</returns>
    public CacheSection GetCacheSection() => _cacheSection;

    private T ReadSection<T>(ClassSection section, BinaryReader reader)
    {
        throw  new NotImplementedException();
    }
}