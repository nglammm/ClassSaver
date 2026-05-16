namespace ClassSaver.Structure;

/// <summary>
/// The cache modes available.
/// </summary>
public enum CacheMode
{
    /// <summary>
    /// No caching applied, this is often good if the class you are serializing
    /// contains little data and the cache data made the file bigger.
    /// </summary>
    None,
    
    /// <summary>
    /// Caches the field name strings and assign it with an alias value to make the data smaller.
    /// </summary>
    Keyword
}